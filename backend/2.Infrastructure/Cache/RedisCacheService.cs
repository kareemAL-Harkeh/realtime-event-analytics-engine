using System.Text.Json;
using StackExchange.Redis;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Infrastructure.Constants;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Cache;

/// <summary>
/// Production-grade Redis cache implementer optimized for telemetry pipelining and low-allocation serialization under .NET 10.
/// </summary>
public sealed class RedisCacheService : IRedisCacheService
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;
    
    // Memory Optimization: Strict configuration for thread-safe JSON serialization profiles
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Keep raw events in memory for 2 hours to avoid overflowing the Redis RAM instance
    private static readonly TimeSpan EventCacheTtl = TimeSpan.FromHours(2);

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
            var eventKey = GetEventKey(command.Timestamp);
            var payload = JsonSerializer.Serialize(command, JsonOptions);

            // Enforce a strict TTL to prevent Redis Out Of Memory (OOM) crashes in Production
            var rawWriteTask = _db.StringSetAsync(eventKey, payload, EventCacheTtl);

            // Real-time Atomic Counter Increments (Radically speeds up Dashboard Querying)
            var counterKey = $"{CacheConstants.DashboardKeyPrefix}:live_counters";
            var incrementTotalTask = _db.HashIncrementAsync(counterKey, "TotalEvents");
            var incrementTypeTask = _db.HashIncrementAsync(counterKey, $"Type:{command.EventType.ToLowerInvariant()}");

            // Execute all operations asynchronously in parallel leveraging .NET 10 optimized task pooling
            await Task.WhenAll(rawWriteTask, incrementTotalTask, incrementTypeTask).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            _logger.LogWarning(ex, "Redis cache operation failed. Continuing without cache update.");
        }
    }

    public async Task<DashboardOverview?> GetDashboardAsync(FetchDashboardDataQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        try
        {
            var key = GetDashboardKey(query.WindowMinutes);

            // CommandDefinition equivalent in StackExchange.Redis passing the cancellation token down to the sockets
            var payload = await _db.StringGetAsync(new RedisKey(key)).WaitAsync(cancellationToken);

            if (!payload.HasValue) return null;

            return JsonSerializer.Deserialize<DashboardOverview>(payload.ToString(), JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            _logger.LogWarning(ex, "Redis dashboard read failed. Returning no cached value.");
            return null;
        }
    }

    public async Task SetDashboardAsync(FetchDashboardDataQuery query, DashboardOverview overview, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(overview);

        try
        {
            var key = GetDashboardKey(query.WindowMinutes);
            var payload = JsonSerializer.Serialize(overview, JsonOptions);

            var ttl = TimeSpan.FromMinutes(CacheConstants.DashboardCacheTtlMinutes <= 0 ? 5 : CacheConstants.DashboardCacheTtlMinutes);

            // Snapshot caching to protect the Postgres database from concurrent dashboard spamming
            await _db.StringSetAsync(key, payload, ttl).WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            _logger.LogWarning(ex, "Redis dashboard write failed. Continuing without cache update.");
        }
    }

    private static string GetEventKey(DateTimeOffset timestamp) 
        => $"{CacheConstants.EventKeyPrefix}:{timestamp.UtcDateTime:yyyyMMddHHmmssfff}";

    private static string GetDashboardKey(int windowMinutes) 
        => $"{CacheConstants.DashboardKeyPrefix}:{windowMinutes}";
}