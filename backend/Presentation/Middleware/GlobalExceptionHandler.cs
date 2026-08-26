using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Middleware;

/// <summary>
/// Last line of defense for anything that escapes the try/catch blocks already
/// present in individual endpoints (EventsEndpoints, DashboardEndpoints) and in
/// EventWriteBackgroundService. Without this, an unhandled exception anywhere
/// else in the pipeline - model binding, middleware, a bug in code we haven't
/// wrapped yet - would fall through to ASP.NET Core's default behavior instead
/// of the same ApiResponse-shaped envelope every other error in this API returns.
///
/// Two deliberate design choices worth calling out:
///
/// 1. This does NOT handle FluentValidation failures. Those are checked
///    explicitly via IValidator.ValidateAsync inside each endpoint and turned
///    into a 400 before anything ever throws. A ValidationException reaching
///    this handler would itself be a bug worth seeing clearly in the logs,
///    not something to quietly reshape into a generic 500.
///
/// 2. It treats a client disconnect differently from a real failure. Now that
///    CancellationToken flows from HttpContext.RequestAborted into the
///    endpoints (EventsEndpoints/DashboardEndpoints), a client closing the
///    connection mid-request surfaces here as an OperationCanceledException.
///    That is expected traffic noise, not a system failure - logging it as an
///    Error would bury real problems in the noise, and writing a response body
///    to an already-closed connection would just throw a second exception.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (IsClientDisconnect(exception, httpContext))
        {
            _logger.LogDebug(
                "Request {Method} {Path} was aborted by the client. TraceId={TraceId}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);

            // Handled, but deliberately nothing written to the response -
            // the client is already gone.
            return true;
        }

        // TraceIdentifier is what ties this specific log entry to whatever we
        // hand back to the caller. If someone reports "I got an error", this ID
        // is how it gets matched to the exact Serilog entry that explains why -
        // without it we'd be grepping logs by timestamp and hoping for the best.
        var traceId = httpContext.TraceIdentifier;

        _logger.LogError(exception,
            "Unhandled exception on {Method} {Path}. TraceId={TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json";

        var response = new ErrorResponse(
            Status: "Error",
            Message: "An unexpected error occurred while processing the request.",
            TraceId: traceId);

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        // true tells the framework "this is fully handled" - it should not also
        // run its own default unhandled-exception behavior on top of ours.
        return true;
    }

    private static bool IsClientDisconnect(Exception exception, HttpContext httpContext)
        => exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested;
}
