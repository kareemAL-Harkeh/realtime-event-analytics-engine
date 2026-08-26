using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Core.Queries;

/// <summary>
/// Covers FetchDashboardDataQueryHandler's cache-aside behavior.
///
/// What this deliberately does NOT test: whether the per-window lock fix
/// (replacing one global static SemaphoreSlim with a lock keyed by
/// WindowMinutes) actually prevents unrelated windows from serializing behind
/// each other. That's a contention/timing property under concurrent load, and
/// a fast sequential unit test calling HandleAsync twice in a row can't
/// honestly demonstrate it either way - proving it would need a real
/// concurrency harness (parallel calls + timing assertions), which is a
/// different, heavier kind of test than what belongs in this file.
///
/// What IS tested here: the simpler, still load-bearing cache-aside contract -
/// a cache hit never touches the repository, and a cache miss populates the
/// cache and queries the repository independently per window.
/// </summary>
public sealed class FetchDashboardDataQueryHandlerTests
{
    private sealed class FakeRedisCacheService : IRedisCacheService
    {
        // Keyed by WindowMinutes, mirroring the real RedisCacheService's
        // GetDashboardKey(windowMinutes) ("dashboard:5" vs "dashboard:60").
        // A single shared field here (the original version of this fake) would
        // make ANY window look like a cache hit the moment ANY other window was
        // cached - not what production actually does, and it's exactly what
        // caused HandleAsync_DifferentWindows_EachQueriesRepositoryIndependently
        // to fail: window 60 was reading back window 5's cached value.
        private readonly Dictionary<int, DashboardOverview> _cache = new();

        public int GetCallCount { get; private set; }
        public int SetCallCount { get; private set; }

        public void Seed(int windowMinutes, DashboardOverview overview) => _cache[windowMinutes] = overview;

        public Task CacheEventAsync(LogEventCommand command, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DashboardOverview?> GetDashboardAsync(
            FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            _cache.TryGetValue(query.WindowMinutes, out var overview);
            return Task.FromResult(overview);
        }

        public Task SetDashboardAsync(
            FetchDashboardDataQuery query, DashboardOverview overview, CancellationToken cancellationToken = default)
        {
            SetCallCount++;
            _cache[query.WindowMinutes] = overview;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventRepository : IEventRepository
    {
        public int QueryCallCount { get; private set; }
        public DashboardOverview Result { get; set; } = new(0, new Dictionary<string, int>(), 0);

        public Task SaveEventsBatchAsync(
            IReadOnlyList<LogEventCommand> commands, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DashboardOverview> GetDashboardOverviewAsync(
            FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
        {
            QueryCallCount++;
            return Task.FromResult(Result);
        }
    }

    [Fact]
    public async Task HandleAsync_CacheHit_NeverQueriesRepository()
    {
        var cache = new FakeRedisCacheService();
        cache.Seed(30, new DashboardOverview(10, new Dictionary<string, int> { ["success"] = 10 }, 100));
        var repository = new FakeEventRepository();
        var handler = new FetchDashboardDataQueryHandler(cache, repository);

        var result = await handler.HandleAsync(new FetchDashboardDataQuery(30));

        Assert.Equal(10, result.TotalEvents);
        Assert.Equal(0, repository.QueryCallCount);
    }

    [Fact]
    public async Task HandleAsync_CacheMiss_QueriesRepositoryAndPopulatesCache()
    {
        var cache = new FakeRedisCacheService();
        var repository = new FakeEventRepository
        {
            Result = new DashboardOverview(42, new Dictionary<string, int> { ["success"] = 42 }, 100)
        };
        var handler = new FetchDashboardDataQueryHandler(cache, repository);

        var result = await handler.HandleAsync(new FetchDashboardDataQuery(30));

        Assert.Equal(42, result.TotalEvents);
        Assert.Equal(1, repository.QueryCallCount);
        Assert.Equal(1, cache.SetCallCount);
    }

    [Fact]
    public async Task HandleAsync_DifferentWindows_EachQueriesRepositoryIndependently()
    {
        var cache = new FakeRedisCacheService();
        var repository = new FakeEventRepository();
        var handler = new FetchDashboardDataQueryHandler(cache, repository);

        await handler.HandleAsync(new FetchDashboardDataQuery(5));
        await handler.HandleAsync(new FetchDashboardDataQuery(60));

        Assert.Equal(2, repository.QueryCallCount);
    }
}
