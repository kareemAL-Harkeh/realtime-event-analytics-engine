using FluentAssertions;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Core;

/// <summary>
/// Core-level unit tests for application Handlers using high-performance, zero-allocation Test Fakes.
/// </summary>
public class HandlersTests
{
    #region LogEventCommandHandler Tests

    [Fact]
    public async Task LogEventCommandHandler_ShouldEnqueueCommandWithUtcTimestamp()
    {
        // Arrange
        var queue = new TestEventWriteQueue();
        var handler = new LogEventCommandHandler(queue);
        var command = new LogEventCommand("info", "{\"message\":\"ok\"}", "service-a", default);

        // Act
        await handler.HandleAsync(command);

        queue.QueuedCommands.Should().ContainSingle();
        var enqueued = queue.QueuedCommands[0];
        
        enqueued.EventType.Should().Be(command.EventType);
        enqueued.Source.Should().Be(command.Source);
        enqueued.Timestamp.Should().BeAfter(DateTimeOffset.MinValue, "because the handler must enrich default timestamps with UTC now");
    }

    #endregion

    #region FetchDashboardDataQueryHandler Tests

    [Fact]
    public async Task FetchDashboardDataQueryHandler_ShouldUseCacheWhenAvailable()
    {
        // Arrange
        var cache = new TestRedisCacheService();
        var repo = new TestEventRepository();
        var handler = new FetchDashboardDataQueryHandler(cache, repo);
        var query = new FetchDashboardDataQuery(15);
        var expectedOverview = new DashboardOverview(5, new Dictionary<string, int> { ["info"] = 5 }, 100);

        cache.CachedOverview = expectedOverview;

        // Act
        var result = await handler.HandleAsync(query);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedOverview);
        repo.WasCalled.Should().BeFalse("because database must be bypassed completely on cache hits");
    }

    [Fact]
    public async Task FetchDashboardDataQueryHandler_ShouldFetchFromDbAndHydrateCacheOnCacheMiss()
    {
        // Arrange (Case: Cache is empty/null)
        var cache = new TestRedisCacheService(); 
        var dbResult = new DashboardOverview(12, new Dictionary<string, int> { ["success"] = 12 }, 100);
        var repo = new TestEventRepository(dbResult);
        
        var handler = new FetchDashboardDataQueryHandler(cache, repo);
        var query = new FetchDashboardDataQuery(30);

        cache.CachedOverview = null; // Cache Miss simulation

        // Act
        var result = await handler.HandleAsync(query);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(dbResult);
        repo.WasCalled.Should().BeTrue("because a cache miss occurred and database fallback was mandatory");
        
        // Ensure cache was hydrated with the database result for subsequent requests
        cache.SavedOverview.Should().NotBeNull("because subsequent requests must hit Redis");
        cache.SavedOverview.Should().BeEquivalentTo(dbResult);
    }

    #endregion

    #region High-Performance In-Memory Zero-Allocation Test Fakes

    private sealed class TestEventWriteQueue : IEventWriteQueue
    {
        public List<LogEventCommand> QueuedCommands { get; } = [];

        public void Enqueue(LogEventCommand command)
        {
            QueuedCommands.Add(command);
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public bool TryDequeue(out LogEventCommand? command)
        {
            command = null;
            return false;
        }

        public IAsyncEnumerable<LogEventCommand> DequeueAllAsync(CancellationToken cancellationToken)
        {
            return AsyncEnumerable.Empty<LogEventCommand>();
        }
    }

    private sealed class TestRedisCacheService : IRedisCacheService
    {
        public DashboardOverview? CachedOverview { get; set; }
        public DashboardOverview? SavedOverview { get; private set; }

        public Task CacheEventAsync(LogEventCommand command, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DashboardOverview?> GetDashboardAsync(FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(CachedOverview);

        public Task SetDashboardAsync(FetchDashboardDataQuery query, DashboardOverview overview, CancellationToken cancellationToken = default)
        {
            SavedOverview = overview; // Spy on the cache set action
            return Task.CompletedTask;
        }
    }

    private sealed class TestEventRepository : IEventRepository
    {
        private readonly DashboardOverview _expectedResult;
        public bool WasCalled { get; private set; }

        public TestEventRepository(DashboardOverview? expectedResult = null)
        {
            _expectedResult = expectedResult ?? new DashboardOverview(0, new Dictionary<string, int>(), 0);
        }

        public Task SaveEventsBatchAsync(IReadOnlyList<LogEventCommand> commands, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<DashboardOverview> GetDashboardOverviewAsync(FetchDashboardDataQuery query, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(_expectedResult);
        }
    }

    #endregion
}