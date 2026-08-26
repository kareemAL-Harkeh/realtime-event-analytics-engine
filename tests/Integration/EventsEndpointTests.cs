using System.Net;
using System.Net.Http.Json;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Presentation.Responses;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Integration;

/// <summary>
/// Exercises POST /api/events through the real HTTP pipeline: authentication,
/// then validation, then the real in-memory queue. See
/// CustomWebApplicationFactory for exactly what is and isn't real here.
/// </summary>
public sealed class EventsEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventsEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static LogEventCommand ValidCommand() => new()
    {
        EventType = "info",
        Payload = "{\"message\":\"integration test\"}",
        Source = "integration-test",
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task Post_WithoutApiKey_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", ValidCommand());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.Equal("Unauthorized", body!.Status);
    }

    [Fact]
    public async Task Post_WithDashboardKey_Returns403()
    {
        // A dashboard-client key trying to POST events - authenticated fine,
        // just doesn't hold the right role. This is HandleForbiddenAsync in
        // ApiKeyAuthenticationHandler, exercised end to end.
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.DashboardApiKey);

        var response = await client.PostAsJsonAsync("/api/events", ValidCommand());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithIngestionKey_InvalidPayload_Returns400()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.IngestionApiKey);

        var invalidCommand = ValidCommand() with { EventType = "" };

        var response = await client.PostAsJsonAsync("/api/events", invalidCommand);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithIngestionKey_ValidPayload_Returns202()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", CustomWebApplicationFactory.IngestionApiKey);

        var response = await client.PostAsJsonAsync("/api/events", ValidCommand());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
