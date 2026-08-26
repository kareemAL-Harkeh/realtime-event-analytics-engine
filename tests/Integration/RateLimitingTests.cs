using System.Net;
using System.Net.Http.Json;
using RealTimeEventAnalyticsEngine.Core.Commands;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Integration;

/// <summary>
/// Uses its own CustomWebApplicationFactory instance (not the shared
/// IClassFixture one in EventsEndpointTests) configured with a near-zero
/// token bucket. The real default (TokenLimit=200) would need 201 requests in
/// a single test to ever observe a 429 - slow, and it would make this test's
/// actual point ("does a 429 happen and carry a Retry-After header")
/// secondary to just generating volume.
/// </summary>
public sealed class RateLimitingTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RateLimitingTests()
    {
        _factory = new CustomWebApplicationFactory
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["RateLimiting:Events:TokenLimit"] = "1",
                ["RateLimiting:Events:TokensPerPeriod"] = "1",
                ["RateLimiting:Events:ReplenishmentSeconds"] = "60"
            }
        };

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.IngestionApiKey);
    }

    private static LogEventCommand ValidCommand() => new()
    {
        EventType = "info",
        Payload = "{}",
        Source = "integration-test",
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task SecondRequest_WithinSameWindow_Returns429()
    {
        var first = await _client.PostAsJsonAsync("/api/events", ValidCommand());
        var second = await _client.PostAsJsonAsync("/api/events", ValidCommand());

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.True(second.Headers.Contains("Retry-After"));
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
