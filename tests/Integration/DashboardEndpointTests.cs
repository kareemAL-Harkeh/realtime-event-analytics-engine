using System.Net;
using System.Net.Http.Json;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Presentation.Responses;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Integration;

/// <summary>
/// Exercises GET /api/dashboard through the real HTTP pipeline. The "happy
/// path" test seeds CustomWebApplicationFactory.Repository directly rather
/// than needing a real Postgres - see CustomWebApplicationFactory for why.
/// </summary>
public sealed class DashboardEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DashboardEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithIngestionKey_Returns403()
    {
        // Mirror image of EventsEndpointTests.Post_WithDashboardKey_Returns403 -
        // an ingestion-client key has no business reading the dashboard either.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.IngestionApiKey);

        var response = await client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidWindowMinutes_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.DashboardApiKey);

        var response = await client.GetAsync("/api/dashboard?windowMinutes=999999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ValidRequest_ReturnsFakeRepositoryData()
    {
        _factory.Repository.Result = new DashboardOverview(
            TotalEvents: 7,
            EventsByType: new Dictionary<string, int> { ["success"] = 7 },
            RecentSuccessRate: 100);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.DashboardApiKey);

        var response = await client.GetAsync("/api/dashboard?windowMinutes=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DashboardOverview>>();
        Assert.Equal(7, body!.Data.TotalEvents);
    }
}
