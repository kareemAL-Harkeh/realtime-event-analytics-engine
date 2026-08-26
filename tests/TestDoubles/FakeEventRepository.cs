using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;

namespace RealTimeEventAnalyticsEngine.Tests.TestDoubles;

/// <summary>
/// Simple in-memory stand-in for IEventRepository, shared by the integration
/// tests to get deterministic HTTP responses without a real Postgres.
///
/// Deliberately mutable and NOT thread-safe: the integration tests here run
/// requests sequentially against a single factory instance per test class, so
/// there is no concurrent access to guard against, and adding locking here
/// would just be unused ceremony that could hide a real bug behind false
/// confidence.
/// </summary>
public sealed class FakeEventRepository : IEventRepository
{
    public int SaveCallCount { get; private set; }
    public int QueryCallCount { get; private set; }
    public List<LogEventCommand> SavedEvents { get; } = new();
    public DashboardOverview Result { get; set; } = new(0, new Dictionary<string, int>(), 0);

    public Task SaveEventsBatchAsync(
        IReadOnlyList<LogEventCommand> commands, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        SavedEvents.AddRange(commands);
        return Task.CompletedTask;
    }

    public Task<DashboardOverview> GetDashboardOverviewAsync(
        FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
    {
        QueryCallCount++;
        return Task.FromResult(Result);
    }
}
