namespace RealTimeEventAnalyticsEngine.Infrastructure.Constants;

/// <summary>
/// Centralized cache configuration constants for sub-millisecond performance optimization
/// </summary>
public static class CacheConstants
{
    /// <summary>Dashboard cache TTL in minutes - balances freshness with reduced DB queries</summary>
    public const int DashboardCacheTtlMinutes = 2;

    /// <summary>Event cache key prefix for Redis key namespacing</summary>
    public const string EventKeyPrefix = "event";

    /// <summary>Dashboard cache key prefix for Redis key namespacing</summary>
    public const string DashboardKeyPrefix = "dashboard";

    /// <summary>Redis channel for pub/sub event notifications (future feature)</summary>
    public const string EventStreamChannel = "events:stream";
}

