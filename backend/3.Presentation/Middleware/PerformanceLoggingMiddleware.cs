using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RealTimeEventAnalyticsEngine.Presentation.Middleware;

/// <summary>
/// Performance middleware that tracks request/response timing for sub-millisecond monitoring
/// Logs detailed timing information to Serilog for performance analysis
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
        var startTime = DateTimeOffset.UtcNow;
        try
        {
            await _next(context);
            var elapsed = DateTimeOffset.UtcNow - startTime;
            
            _logger.LogInformation(
                "Request completed: {Method} {Path} - Status: {StatusCode} - Duration: {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            var elapsed = DateTimeOffset.UtcNow - startTime;
            _logger.LogError(ex,
                "Request failed: {Method} {Path} - Duration: {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                elapsed.TotalMilliseconds);
            throw;
        }
    }
}
