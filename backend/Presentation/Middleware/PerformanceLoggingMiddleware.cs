using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RealTimeEventAnalyticsEngine.Presentation.Middleware;

/// <summary>
/// Simple middleware that measures and logs the duration of each request.
///
/// NOTE: this middleware only owns timing, not error reporting. It used to log
/// the full exception (with stack trace) on failure, but now that
/// <see cref="GlobalExceptionHandler"/> exists and owns that responsibility -
/// including writing the client-facing TraceId - logging the exception here too
/// meant every unhandled failure showed up twice in Serilog with two different
/// messages for the same event. This middleware runs "inside" the exception
/// handler in the pipeline (see Program.cs: UseExceptionHandler is registered
/// first), so by the time GlobalExceptionHandler catches anything thrown here,
/// this middleware has already had its chance to record how long the request
/// took before it failed.
/// </summary>
public sealed class PerformanceLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceLoggingMiddleware> _logger;

    public PerformanceLoggingMiddleware(RequestDelegate next, ILogger<PerformanceLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
            sw.Stop();

            _logger.LogInformation(
                "{Method} {Path} responded {StatusCode} in {ElapsedMs:0.00} ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception)
        {
            sw.Stop();

            // Deliberately not passing the exception object here - see the class
            // summary. GlobalExceptionHandler logs the full exception, stack
            // trace, and TraceId once, higher up the pipeline. We only add the
            // one piece of information it doesn't have: how long the request
            // ran before it blew up.
            _logger.LogWarning(
                "{Method} {Path} failed after {ElapsedMs:0.00} ms",
                context.Request.Method,
                context.Request.Path,
                sw.Elapsed.TotalMilliseconds);

            throw;
        }
    }
}
