namespace RealTimeEventAnalyticsEngine.Core.Queries;

/// <summary>
/// Query contract specifying the look-back window constraint for analytics.
/// Default window covers 30 days to surface all seeded and live telemetry data.
/// </summary>
public sealed record FetchDashboardDataQuery(int WindowMinutes = 43200); // 43200 = 30 days

/// <summary>
/// High-density analytical immutable snapshot representing current system telemetry status.
/// </summary>
public sealed record DashboardOverview(
    int TotalEvents,
    IReadOnlyDictionary<string, int> EventsByType,
    int RecentSuccessRate
);