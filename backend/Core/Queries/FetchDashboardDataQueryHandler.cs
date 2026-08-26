using System.Collections.Concurrent;
using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Core.Queries;

/// <summary>
/// Handles dashboard queries using the classic Cache-Aside pattern
/// with a simple in-process lock to reduce cache stampede.
/// 
/// Note: These locks only protect a single instance.
/// When you scale out to multiple pods/instances you should replace
/// this with a distributed lock (Redis) or accept a short stampede.
/// </summary>
public sealed class FetchDashboardDataQueryHandler
{
    private readonly IRedisCacheService _cache;
    private readonly IEventRepository _repository;

    // One gate per distinct WindowMinutes value rather than a single shared gate.
    // A single static SemaphoreSlim would serialize every dashboard query through
    // the same lane regardless of window - a request for "last 5 minutes" would
    // queue up behind a request for "last 30 days" even though they hit entirely
    // different Redis cache keys and have nothing to do with each other. Keying
    // the gate by window means only requests actually competing for the same
    // cache entry (and the same DB query) serialize against each other.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> DatabaseLocks = new();

    public FetchDashboardDataQueryHandler(
        IRedisCacheService cache,
        IEventRepository repository)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DashboardOverview> HandleAsync(
        FetchDashboardDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fast path: try the cache first
        var dashboard = await _cache.GetDashboardAsync(query, cancellationToken);
        if (dashboard is not null)
        {
            return dashboard;
        }

        // Cache miss → acquire the gate for THIS window to limit concurrent
        // database hits for the same query, without blocking unrelated windows.
        var gate = DatabaseLocks.GetOrAdd(query.WindowMinutes, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            // Double-check: another thread may have already populated the cache
            dashboard = await _cache.GetDashboardAsync(query, cancellationToken);
            if (dashboard is not null)
            {
                return dashboard;
            }

            // Slow path: load from the database
            dashboard = await _repository.GetDashboardOverviewAsync(query, cancellationToken);

            // Populate the cache for the next requests
            await _cache.SetDashboardAsync(query, dashboard, cancellationToken);

            return dashboard;
        }
        finally
        {
            gate.Release();
        }
    }
}
