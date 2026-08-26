using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Infrastructure.Constants;
using StackExchange.Redis;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Cache;

/// <summary>
/// Redis-backed cache service.
/// 
/// - Stores pre-computed dashboard snapshots.
/// - Maintains lightweight live counters for incoming events.
/// - Designed to fail gracefully (never throws to the caller).
/// </summary>
public sealed class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public RedisCacheService(IConnectionMultiplexer multiplexer, ILogger<RedisCacheService> logger)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _db = multiplexer.GetDatabase();
    }

    public async Task CacheEventAsync(LogEventCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            var counterKey = $"{CacheConstants.DashboardKeyPrefix}:live_counters";

            // NOTE FOR WHOEVER TOUCHES THIS NEXT: nothing in the codebase currently
            // reads these counters back - GetDashboardOverviewAsync queries Postgres
            // directly on every cache miss instead. As it stands, every single
            // ingested event pays for a Redis round trip that produces a value no
            // one consumes. Either wire FetchDashboardDataQueryHandler to use this
            // hash as a genuine sub-second real-time fast path (e.g. for very short
            // windows), or remove it so we stop paying for it. Until that decision
            // is made, we at least keep it bounded with a rolling expiry below -
            // previously this hash had no TTL at all and would have grown forever.
            var totalTask = _db.HashIncrementAsync(counterKey, "TotalEvents");
            var typeTask = _db.HashIncrementAsync(counterKey, $"Type:{command.EventType.ToLowerInvariant()}");

            // Sliding expiry: as long as events keep flowing the key stays alive;
            // if ingestion goes quiet for a day, it expires and starts fresh next
            // time - reasonable behavior for something meant to represent "live" state.
            var expiryTask = _db.KeyExpireAsync(counterKey, TimeSpan.FromHours(24));

            await Task.WhenAll(totalTask, typeTask, expiryTask).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to update Redis live counters for event type {EventType}", command.EventType);
        }
    }

    public async Task<DashboardOverview?> GetDashboardAsync(
        FetchDashboardDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var key = GetDashboardKey(query.WindowMinutes);
            var payload = await _db.StringGetAsync(key).ConfigureAwait(false);

            if (payload.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<DashboardOverview>((string)payload!, JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to read dashboard from Redis (window: {Window} min)", query.WindowMinutes);
            return null;
        }
    }

    public async Task SetDashboardAsync(
        FetchDashboardDataQuery query,
        DashboardOverview overview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(overview);

        try
        {
            var key = GetDashboardKey(query.WindowMinutes);
            var payload = JsonSerializer.Serialize(overview, JsonOptions);

            var ttlMinutes = CacheConstants.DashboardCacheTtlMinutes > 0
                ? CacheConstants.DashboardCacheTtlMinutes
                : 2;

            await _db.StringSetAsync(key, payload, TimeSpan.FromMinutes(ttlMinutes)).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to write dashboard to Redis (window: {Window} min)", query.WindowMinutes);
        }
    }

    private static string GetDashboardKey(int windowMinutes)
        => $"{CacheConstants.DashboardKeyPrefix}:{windowMinutes}";
}
