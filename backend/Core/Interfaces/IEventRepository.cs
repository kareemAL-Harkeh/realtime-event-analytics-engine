using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Core.Interfaces;

/// <summary>
/// Abstraction over the analytical storage.
/// 
/// Responsible for two main jobs:
/// 1. High-speed batch ingestion of events.
/// 2. Efficient querying of pre-aggregated dashboard data.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Persists a batch of events in a single round-trip.
    /// Implementations should prefer bulk insert techniques (e.g. UNNEST, COPY, or EF bulk extensions).
    /// </summary>
    Task SaveEventsBatchAsync(
        IReadOnlyList<LogEventCommand> commands,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the calculated dashboard overview for the requested time window and filters.
    /// Should be optimized for read performance (indexes, materialized views, or pre-aggregated tables).
    /// </summary>
    Task<DashboardOverview> GetDashboardOverviewAsync(
        FetchDashboardDataQuery query,
        CancellationToken cancellationToken = default);
}