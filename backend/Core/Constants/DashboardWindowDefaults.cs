namespace RealTimeEventAnalyticsEngine.Core.Constants;

/// <summary>
/// Single source of truth for the dashboard time-window bounds.
///
/// Before this existed, the value 43200 (30 days) was duplicated in three
/// unrelated places: the query record's default parameter, the validator's
/// upper bound, and a literal inside the endpoint delegate. Nothing kept them
/// in sync - changing the max window meant remembering to update all three,
/// and missing one would silently create a mismatch between what the endpoint
/// defaults to and what the validator actually allows.
/// </summary>
public static class DashboardWindowDefaults
{
    public const int MinWindowMinutes = 1;
    public const int MaxWindowMinutes = 43_200; // 30 days
    public const int DefaultWindowMinutes = MaxWindowMinutes;
}
