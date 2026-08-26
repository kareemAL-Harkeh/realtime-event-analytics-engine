using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Core.Interfaces;

/// <summary>
/// Abstraction over the distributed cache (Redis).
/// 
/// Responsibilities:
/// - Fast storage and retrieval of pre-computed dashboard snapshots.
/// - Lightweight real-time counters / aggregations for incoming events.
/// </summary>
public interface IRedisCacheService
{
    /// <summary>
    /// Updates real-time analytical structures (counters, recent events, etc.)
    /// when a new event arrives. Should be fast and non-blocking.
    /// </summary>
    Task CacheEventAsync(LogEventCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to get a previously cached dashboard overview for the given query.
    /// Returns null on cache miss.
    /// </summary>
    Task<DashboardOverview?> GetDashboardAsync(
        FetchDashboardDataQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a fully computed dashboard overview so subsequent requests can be served quickly.
    /// </summary>
    Task SetDashboardAsync(
        FetchDashboardDataQuery query,
        DashboardOverview overview,
        CancellationToken cancellationToken = default);
}