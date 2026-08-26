using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Extensions;

/// <summary>
/// Registers rate limiting policies for the public API surface.
///
/// This lives in the Presentation layer rather than alongside the other
/// service registrations in Infrastructure.Extensions.ServiceCollectionExtensions
/// on purpose. The 429 response body it builds is an ApiResponse - a
/// Presentation-layer contract - and rate limiting is fundamentally a "how do
/// we respond to a client" concern (status codes, response envelopes,
/// Retry-After headers), not an external-system-integration concern like the
/// DB/Redis/queue registrations Infrastructure actually owns. Putting it there
/// would mean Infrastructure reaching upward into Presentation just because
/// that's where DI registration "usually happens" in this project - the same
/// kind of layering slip already caught once on the persistence side
/// (EventWriteRepository exposing seeding methods IEventRepository doesn't
/// declare), this time on the response-shaping side instead.
/// </summary>
public static class RateLimitingExtensions
{
    public static IServiceCollection AddRateLimitingPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            ConfigureEventsIngestionPolicy(options, configuration);
            ConfigureDashboardReadPolicy(options, configuration);
            ConfigureRejectionResponse(options);
        });

        return services;
    }

    // --- /api/events: high-throughput, bursty ingestion ---
    //
    // Ingestion traffic is naturally bursty - a service might legitimately emit
    // a burst of events around a deploy or an incident. A fixed window would
    // punish a burst that happens to straddle a window boundary even when the
    // sustained rate is perfectly fine. A token bucket tolerates short bursts up
    // to the bucket size while still capping the sustained rate through steady
    // replenishment, which fits this traffic shape much better.
    //
    // QueueLimit is deliberately 0: EventWriteQueue.TryEnqueue never blocks the
    // caller either - a full channel returns false immediately and the endpoint
    // answers with 503 right away. Letting the rate limiter queue requests here
    // would reintroduce the exact "make the caller wait" problem the write path
    // was designed to avoid, just one layer earlier in the pipeline.
    private static void ConfigureEventsIngestionPolicy(RateLimiterOptions options, IConfiguration configuration)
    {
        var tokenLimit = configuration.GetValue("RateLimiting:Events:TokenLimit", 200);
        var tokensPerPeriod = configuration.GetValue("RateLimiting:Events:TokensPerPeriod", 200);
        var replenishmentSeconds = configuration.GetValue("RateLimiting:Events:ReplenishmentSeconds", 1);

        options.AddPolicy(RateLimitingPolicies.EventsIngestion, httpContext =>
            RateLimitPartition.GetTokenBucketLimiter(
                GetPartitionKey(httpContext),
                _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = tokenLimit,
                    TokensPerPeriod = tokensPerPeriod,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(replenishmentSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));
    }

    // --- /api/dashboard: cached reads ---
    //
    // A dashboard request that misses Redis falls through to a real Postgres
    // query. Nothing stops a caller from varying `windowMinutes` on every
    // request, which sidesteps the cache entirely - every distinct window is
    // its own cache key AND its own lock in FetchDashboardDataQueryHandler - and
    // would force a DB hit on every single call. This endpoint doesn't need
    // burst tolerance the way ingestion does; it needs a flat ceiling, so a
    // fixed window is enough.
    private static void ConfigureDashboardReadPolicy(RateLimiterOptions options, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:Dashboard:PermitLimit", 30);
        var windowSeconds = configuration.GetValue("RateLimiting:Dashboard:WindowSeconds", 60);

        options.AddPolicy(RateLimitingPolicies.DashboardRead, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                }));
    }

    // Consistent with every other error shape this API returns - the same
    // ApiResponse envelope as the 503 "QueueFull" response in EventsEndpoints -
    // so a caller never needs a special case just for 429s.
    private static void ConfigureRejectionResponse(RateLimiterOptions options)
    {
        options.OnRejected = async (context, cancellationToken) =>
        {
            var httpContext = context.HttpContext;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                httpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString();
            }

            httpContext.Response.ContentType = "application/json";

            httpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("RateLimiting")
                .LogWarning(
                    "Rate limit exceeded for {Method} {Path} from {PartitionKey}",
                    httpContext.Request.Method,
                    httpContext.Request.Path,
                    GetPartitionKey(httpContext));

            var response = new ApiResponse<object>(
                Status: "RateLimited",
                Data: null!,
                Message: "Too many requests. Please slow down and retry later.");

            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
        };
    }

    // Now that API key authentication exists (see Authentication/), we key the
    // bucket on the caller's authenticated identity instead of the raw IP
    // whenever it's available. This is exactly the upgrade flagged as a "known
    // limitation" when rate limiting was first built: several legitimate
    // services sitting behind the same reverse proxy or NAT gateway would
    // otherwise share (and unfairly drain) one IP's bucket. Identity is the
    // thing that actually identifies "one caller" here, not the network path
    // it happened to arrive over.
    //
    // The IP fallback stays for anything that reaches this endpoint without a
    // recognized key - UseAuthentication runs before UseRateLimiter in the
    // pipeline (see Program.cs), so an unauthenticated caller still gets
    // *a* bucket rather than bypassing rate limiting entirely; it just won't
    // be a fair, per-client one.
    private static string GetPartitionKey(HttpContext httpContext)
    {
        var clientId = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            : null;

        return clientId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

/// <summary>
/// Policy name constants shared between the registration above and the
/// endpoints that opt into them (EventsEndpoints, DashboardEndpoints). Plain
/// string literals repeated in two unrelated places would be the exact same
/// "magic value duplicated across layers" mistake already fixed once with
/// DashboardWindowDefaults - one typo in one of the two places and the policy
/// silently stops applying.
/// </summary>
public static class RateLimitingPolicies
{
    public const string EventsIngestion = "events-ingestion";
    public const string DashboardRead = "dashboard-read";
}
