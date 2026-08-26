using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealTimeEventAnalyticsEngine.Presentation.Responses;

namespace RealTimeEventAnalyticsEngine.Presentation.Authentication;

/// <summary>
/// Options for the API key scheme. Deliberately minimal - there's nothing to
/// configure at the framework level; the actual key list is read straight from
/// configuration inside the handler below.
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

/// <summary>
/// The only two roles an API key can hold in this system - one per trust
/// boundary that actually exists here: backend services pushing events in,
/// and dashboard clients only reading the aggregated result.
///
/// There's deliberately no "admin" concept, no user-management, no password
/// hashing. This is server-to-server / trusted-frontend auth for one internal
/// system, not a general-purpose identity provider. Reaching for full ASP.NET
/// Core Identity would mean pulling in a user store, EF Core, and a login UI
/// for a problem that's really just "does this caller hold one of a small
/// number of pre-shared secrets" - a mismatch with how lightweight the rest of
/// this project's infrastructure deliberately is (Dapper over EF, one Postgres
/// table, no ORM). If this system ever grows real human user accounts with
/// their own logins, that's a genuinely different problem worth its own design
/// - not something to bolt onto this.
/// </summary>
public static class ApiKeyRoles
{
    public const string IngestionClient = "ingestion-client";
    public const string DashboardClient = "dashboard-client";
}

/// <summary>
/// Authenticates requests carrying a pre-shared key in the X-Api-Key header
/// against the list configured under "ApiKeys" in appsettings/environment.
///
/// Expected configuration shape:
/// <code>
/// "ApiKeys": [
///   { "Key": "&lt;secret&gt;", "Name": "order-service",     "Role": "ingestion-client" },
///   { "Key": "&lt;secret&gt;", "Name": "dashboard-frontend", "Role": "dashboard-client" }
/// ]
/// </code>
///
/// The key list is loaded once when this handler is constructed rather than
/// re-read from IConfiguration on every request - the key list changing means
/// a redeploy anyway (or at minimum a config reload), so there's no reason to
/// pay a lookup cost on the hot ingestion path for every single event.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IReadOnlyDictionary<string, ApiKeyClient> _clientsByKey;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _clientsByKey = LoadClients(configuration);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            // No key presented at all isn't this handler's call to reject -
            // that's exactly what RequireAuthorization on the endpoint is for.
            // Returning NoResult (not Fail) keeps this handler reusable if a
            // genuinely anonymous endpoint is ever added later.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!_clientsByKey.TryGetValue(providedKey.ToString(), out var client))
        {
            Logger.LogWarning(
                "Rejected request to {Path} with an unrecognized API key from {RemoteIp}",
                Request.Path,
                Context.Connection.RemoteIpAddress);

            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        // NameIdentifier carries the client's NAME, never the key itself - the
        // key should never end up in a claim, a log line, or anything that
        // might get serialized somewhere downstream.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, client.Name),
            new Claim(ClaimTypes.Role, client.Role)
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Runs when an endpoint requires auth and none/invalid was presented (401).
    /// Overridden so a missing/bad API key returns the same ApiResponse envelope
    /// as every other rejection in this API (validation errors, QueueFull,
    /// RateLimited) - a caller shouldn't need a special case just because the
    /// framework's default 401 has no body.
    /// </summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        var response = new ApiResponse<object>(
            Status: "Unauthorized",
            Data: null!,
            Message: $"A valid API key is required in the '{ApiKeyAuthenticationOptions.HeaderName}' header.");

        return Response.WriteAsJsonAsync(response);
    }

    /// <summary>
    /// Runs when the caller authenticated fine but doesn't hold the role the
    /// endpoint's policy requires (403) - e.g. a dashboard-client key trying to
    /// POST /api/events. Same reasoning as HandleChallengeAsync above.
    /// </summary>
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";

        var response = new ApiResponse<object>(
            Status: "Forbidden",
            Data: null!,
            Message: "This API key does not have permission to access this endpoint.");

        return Response.WriteAsJsonAsync(response);
    }

    private static IReadOnlyDictionary<string, ApiKeyClient> LoadClients(IConfiguration configuration)
    {
        var clients = new Dictionary<string, ApiKeyClient>(StringComparer.Ordinal);

        foreach (var entry in configuration.GetSection("ApiKeys").GetChildren())
        {
            var key = entry["Key"];
            var name = entry["Name"];
            var role = entry["Role"];

            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(role))
                continue;

            clients[key] = new ApiKeyClient(name, role);
        }

        return clients;
    }

    private sealed record ApiKeyClient(string Name, string Role);
}
