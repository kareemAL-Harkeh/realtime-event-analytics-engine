using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Tests.TestDoubles;

/// <summary>
/// In-memory stand-in for IRedisCacheService, keyed by WindowMinutes to
/// mirror the real RedisCacheService's per-window cache keys.
///
/// This is the same lesson learned the hard way in
/// FetchDashboardDataQueryHandlerTests: a single shared field here would make
/// every window look like a cache hit for whatever was cached first, which is
/// not what production Redis actually does.
/// </summary>
public sealed class FakeRedisCacheService : IRedisCacheService
{
    private readonly Dictionary<int, DashboardOverview> _cache = new();

    public int EventCacheCallCount { get; private set; }

    public Task CacheEventAsync(LogEventCommand command, CancellationToken cancellationToken = default)
    {
        EventCacheCallCount++;
        return Task.CompletedTask;
    }

    public Task<DashboardOverview?> GetDashboardAsync(
        FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(query.WindowMinutes, out var overview);
        return Task.FromResult(overview);
    }

    public Task SetDashboardAsync(
        FetchDashboardDataQuery query, DashboardOverview overview, CancellationToken cancellationToken = default)
    {
        _cache[query.WindowMinutes] = overview;
        return Task.CompletedTask;
    }
}
