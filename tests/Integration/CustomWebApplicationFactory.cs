using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Presentation.Authentication;
using RealTimeEventAnalyticsEngine.Tests.TestDoubles;

namespace RealTimeEventAnalyticsEngine.Tests.Integration;

/// <summary>
/// Boots the real ASP.NET Core pipeline (auth, rate limiting, validation,
/// exception handling, routing) fully in-memory, WITHOUT Docker or a real
/// Postgres/Redis. Two things make this possible without the host crashing or
/// every request failing outright:
///
/// 1. DatabaseInitializer's startup call is already wrapped in a try/catch in
///    Program.cs (a fix from earlier in this project) - a missing or
///    unreachable database at startup just logs a warning, it never crashes
///    the host. That resilience is what makes an infra-free test host viable
///    at all; without it, this whole approach would be a non-starter.
///
/// 2. IEventRepository and IRedisCacheService are swapped here for the same
///    in-memory fakes already used in the unit tests - not because the real
///    implementations are wrong, but because this test suite's job is to
///    prove the HTTP pipeline behaves correctly (auth -> validation -> rate
///    limiting -> handler), not to re-prove Postgres/Redis integration. That
///    is a distinct, heavier kind of test, deliberately deferred - see the
///    personal notes file for the reasoning.
///
/// What is NOT swapped: IEventWriteQueue stays the real in-memory Channel
/// implementation, and the real EventWriteBackgroundService keeps running as
/// a hosted service. POST /api/events genuinely flows through the real queue -
/// it just never gets far enough in the background to touch a real database.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string IngestionApiKey = "test-ingestion-key";
    public const string DashboardApiKey = "test-dashboard-key";

    public FakeEventRepository Repository { get; } = new();
    public FakeRedisCacheService Cache { get; } = new();

    /// <summary>
    /// Lets an individual test class override configuration - most notably
    /// RateLimiting:* values. The real defaults (TokenLimit=200) would need
    /// 201 requests in a single test to ever observe a 429, which is slow and
    /// makes the test's actual intent secondary to just generating volume.
    /// See RateLimitingTests for the class that uses this.
    /// </summary>
    public Dictionary<string, string?> ConfigurationOverrides { get; init; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var configValues = new Dictionary<string, string?>
            {
                ["ApiKeys:0:Key"] = IngestionApiKey,
                ["ApiKeys:0:Name"] = "test-ingestion-client",
                ["ApiKeys:0:Role"] = ApiKeyRoles.IngestionClient,
                ["ApiKeys:1:Key"] = DashboardApiKey,
                ["ApiKeys:1:Name"] = "test-dashboard-client",
                ["ApiKeys:1:Role"] = ApiKeyRoles.DashboardClient
            };

            foreach (var (key, value) in ConfigurationOverrides)
            {
                configValues[key] = value;
            }

            config.AddInMemoryCollection(configValues);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventRepository>();
            services.AddSingleton<IEventRepository>(Repository);

            services.RemoveAll<IRedisCacheService>();
            services.AddSingleton<IRedisCacheService>(Cache);
        });
    }
}
