using RealTimeEventAnalyticsEngine.Core.Constants;

namespace RealTimeEventAnalyticsEngine.Core.Queries;

/// <summary>
/// Query that defines the time window used to build the dashboard overview.
/// Default window is 30 days (43200 minutes) - see <see cref="DashboardWindowDefaults"/>.
/// </summary>
public sealed record FetchDashboardDataQuery(int WindowMinutes = DashboardWindowDefaults.DefaultWindowMinutes);

/// <summary>
/// Immutable snapshot of the current analytical state of the system.
/// Designed to be cheap to serialize and cache.
/// </summary>
public sealed record DashboardOverview(
    /// <summary>
    /// Total number of events inside the requested time window.
    /// </summary>
    int TotalEvents,

    /// <summary>
    /// Breakdown of events grouped by EventType.
    /// </summary>
    IReadOnlyDictionary<string, int> EventsByType,

    /// <summary>
    /// Approximate success rate (0-100) based on recent events.
    /// This is a simple derived metric and can be refined later.
    /// </summary>
    int RecentSuccessRate
);
