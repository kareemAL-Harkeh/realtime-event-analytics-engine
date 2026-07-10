using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Core.Interfaces;

/// <summary>
/// Unified analytical storage abstraction governing high-speed ingestion and viewport querying.
/// </summary>
public interface IEventRepository
{
    /// <summary>Performs high-velocity asynchronous array unnesting into PostgreSQL storage.</summary>
    Task SaveEventsBatchAsync(IReadOnlyList<LogEventCommand> commands, CancellationToken cancellationToken);

    /// <summary>Fetches calculated time-window dashboard matrices via dynamic single-trip multi-grids.</summary>
    Task<DashboardOverview> GetDashboardOverviewAsync(FetchDashboardDataQuery query, CancellationToken cancellationToken);
}