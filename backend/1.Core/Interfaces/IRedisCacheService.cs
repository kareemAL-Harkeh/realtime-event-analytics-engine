using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Core.Interfaces;

/// <summary>
/// High-velocity Cache abstraction protecting relational storage and driving immediate dashboard metrics.
/// </summary>
public interface IRedisCacheService
{
    /// <summary>
    /// Pipelines incoming telemetry into Redis structures and increments analytical atomic counters with an enforced TTL.
    /// </summary>
    Task CacheEventAsync(LogEventCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the pre-aggregated or snapshotted dashboard analytics viewport.
    /// </summary>
    Task<DashboardOverview?> GetDashboardAsync(FetchDashboardDataQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Snapshots the fully computed analytical view inside memory for fast distribution.
    /// </summary>
    Task SetDashboardAsync(FetchDashboardDataQuery query, DashboardOverview overview, CancellationToken cancellationToken = default);
}