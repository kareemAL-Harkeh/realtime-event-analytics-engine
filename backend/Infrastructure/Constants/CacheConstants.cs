namespace RealTimeEventAnalyticsEngine.Infrastructure.Constants;

/// <summary>
/// Centralized Redis key prefixes and cache settings.
/// </summary>
public static class CacheConstants
{
    /// <summary>
    /// How long a computed dashboard snapshot stays in Redis (in minutes).
    /// </summary>
    public const int DashboardCacheTtlMinutes = 2;

    /// <summary>
    /// Prefix used for dashboard snapshot keys.
    /// </summary>
    public const string DashboardKeyPrefix = "dashboard";

    /// <summary>
    /// Prefix kept for possible future per-event caching or streams.
    /// </summary>
    public const string EventKeyPrefix = "event";

    /// <summary>
    /// Reserved for future Redis Pub/Sub or Streams usage.
    /// </summary>
    public const string EventStreamChannel = "events:stream";
}