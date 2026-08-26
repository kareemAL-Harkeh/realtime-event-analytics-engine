namespace RealTimeEventAnalyticsEngine.Presentation.Authentication;

/// <summary>
/// Policy name constants shared between the AddAuthorizationBuilder
/// registration in Program.cs and the endpoints that opt into them
/// (EventsEndpoints, DashboardEndpoints). Same reasoning as
/// RateLimitingPolicies: a string literal typo'd differently in the two places
/// fails silently at runtime instead of loudly at compile time.
/// </summary>
public static class AuthorizationPolicies
{
    public const string IngestionClient = "IngestionClient";
    public const string DashboardClient = "DashboardClient";
}
