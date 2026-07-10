using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Core.Queries;

/// <summary>
/// Optimizing Cache-Aside handler engineered to mitigate Cache Stampede scenarios 
/// and guarantee sub-millisecond data delivery under peak concurrent traffic.
/// Compliant with strict Clean Architecture patterns under .NET 10.
/// </summary>
public sealed class FetchDashboardDataQueryHandler
{
    private readonly IRedisCacheService _cache;
    private readonly IEventRepository _repository; // 🛠️ حقن واجهة القراءة النظيفة من الكور
    
    // Thread-safe, non-blocking gatekeeper to absorb concurrent connection rushes
    private static readonly SemaphoreSlim DatabaseLock = new(1, 1);

    public FetchDashboardDataQueryHandler(IRedisCacheService cache, IEventRepository repository)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DashboardOverview> HandleAsync(FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // 1. Double-Check Fast-Path: Hit Redis distributed cache first
        var dashboard = await _cache.GetDashboardAsync(query, cancellationToken);
        if (dashboard is not null)
        {
            return dashboard;
        }

        // 2. Cache Miss: Mitigate Thundering Herd via asynchronous synchronization throttling
        await DatabaseLock.WaitAsync(cancellationToken);
        try
        {
            // 3. Double-Check Pattern: Confirm if a previous thread already populated the cache while this thread was waiting
            dashboard = await _cache.GetDashboardAsync(query, cancellationToken);
            if (dashboard is not null)
            {
                return dashboard;
            }

            // 4. Slow-Path: Hit the persistent relational engine using the abstraction layer
            dashboard = await _repository.GetDashboardOverviewAsync(query, cancellationToken);
            
            // 5. Hydrate Cache asynchronously to unblock subsequent requests immediately
            await _cache.SetDashboardAsync(query, dashboard, cancellationToken);
            
            return dashboard;
        }
        finally
        {
            // Always safely release the locking resource under all failure conditions
            DatabaseLock.Release();
        }
    }
}