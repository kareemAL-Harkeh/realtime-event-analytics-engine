using System.Data;
using System.Data.Common;
using Bogus;
using Dapper;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Infrastructure.Cache;
using RealTimeEventAnalyticsEngine.Infrastructure.Constants;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// High-performance data repository utilizing Dapper for raw SQL execution.
/// Optimized for low-latency batch processing and analytical grid aggregation.
/// </summary>
public sealed class EventWriteRepository : IEventRepository
{
    private readonly IEventAnalyticsDbContext _dbContext;

    private static readonly string[] EventTypes = ["success", "error", "warning", "info", "critical"];
    private static readonly string[] Sources    =
    [
        "order-service", "payment-service", "analytics-service",
        "identity-server", "monitoring-agent", "notification-service",
        "inventory-service", "gateway-proxy"
    ];

    public EventWriteRepository(IEventAnalyticsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureEventTableExistsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var sql = $@"
            CREATE TABLE IF NOT EXISTS {DbConstants.EventTable} (
                {DbConstants.EventIdColumn}   UUID        PRIMARY KEY,
                {DbConstants.EventTypeColumn} TEXT        NOT NULL,
                {DbConstants.TimestampColumn} TIMESTAMPTZ NOT NULL,
                {DbConstants.PayloadColumn}   TEXT,
                {DbConstants.SourceColumn}    TEXT        NOT NULL
            );";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<bool> HasAnyEventsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var sql   = $"SELECT COUNT(1) FROM {DbConstants.EventTable};";
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return count > 0;
    }

    /// <summary>
    /// Seeds 1,000 realistic synthetic telemetry events using Bogus.
    /// Timestamps distributed over last 30 days to match the default 43200-minute query window.
    /// </summary>
    public async Task SeedSampleEventsAsync(CancellationToken cancellationToken = default)
    {
        if (await HasAnyEventsAsync(cancellationToken)) return;

        var faker = new Faker<LogEventCommand>()
            .CustomInstantiator(f =>
            {
                var eventType = f.PickRandom(EventTypes);
                var source    = f.PickRandom(Sources);

                var payload = eventType switch
                {
                    "error"    => $"{{\"message\":\"{f.System.Exception().Message}\",\"code\":{f.Random.Int(400, 503)}}}",
                    "warning"  => $"{{\"message\":\"{f.Lorem.Sentence()}\",\"threshold\":{f.Random.Int(75, 95)}}}",
                    "critical" => $"{{\"message\":\"Critical failure in {f.System.FileName()}\",\"severity\":\"HIGH\"}}",
                    "info"     => $"{{\"message\":\"{f.Lorem.Sentence()}\"}}",
                    _          => $"{{\"message\":\"{f.Hacker.Phrase()}\"}}",
                };

                return new LogEventCommand(
                    eventType,
                    payload,
                    source,
                    f.Date.BetweenOffset(
                        DateTimeOffset.UtcNow.AddDays(-30),
                        DateTimeOffset.UtcNow)
                );
            });

        var syntheticEvents = faker.Generate(1000);
        await SaveEventsBatchAsync(syntheticEvents, cancellationToken);
    }

    public async Task SaveEventAsync(LogEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var sql = $@"
            INSERT INTO {DbConstants.EventTable}
            ({DbConstants.EventIdColumn}, {DbConstants.EventTypeColumn}, {DbConstants.TimestampColumn}, {DbConstants.PayloadColumn}, {DbConstants.SourceColumn})
            VALUES (@Id, @EventType, @Timestamp, @Payload, @Source);";

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = Guid.NewGuid(),
            command.EventType,
            command.Timestamp,
            command.Payload,
            command.Source
        }, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Performs a high-velocity bulk insert using PostgreSQL UNNEST function.
    /// Single round-trip regardless of batch size.
    /// </summary>
    public async Task SaveEventsBatchAsync(IReadOnlyList<LogEventCommand> commands, CancellationToken cancellationToken)
    {
        if (commands == null || commands.Count == 0) return;

        using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        var sql = $@"
            INSERT INTO {DbConstants.EventTable}
            ({DbConstants.EventIdColumn}, {DbConstants.EventTypeColumn}, {DbConstants.TimestampColumn}, {DbConstants.PayloadColumn}, {DbConstants.SourceColumn})
            SELECT * FROM UNNEST(@Ids, @EventTypes, @Timestamps, @Payloads, @Sources);";

        var count      = commands.Count;
        var ids        = new Guid[count];
        var eventTypes = new string[count];
        var timestamps = new DateTimeOffset[count];
        var payloads   = new string[count];
        var sources    = new string[count];

        for (int i = 0; i < count; i++)
        {
            ids[i]        = Guid.NewGuid();
            eventTypes[i] = commands[i].EventType;
            timestamps[i] = commands[i].Timestamp;
            payloads[i]   = commands[i].Payload;
            sources[i]    = commands[i].Source;
        }

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Ids        = ids,
            EventTypes = eventTypes,
            Timestamps = timestamps,
            Payloads   = payloads,
            Sources    = sources
        }, cancellationToken: cancellationToken));
    }

    public async Task<DashboardOverview> GetDashboardOverviewAsync(
        FetchDashboardDataQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var timeThreshold = DateTimeOffset.UtcNow.AddMinutes(-query.WindowMinutes);

        var sql = $@"
            SELECT COUNT(1)
            FROM {DbConstants.EventTable}
            WHERE {DbConstants.TimestampColumn} >= @Threshold;

            SELECT {DbConstants.EventTypeColumn} AS EventType, COUNT(1) AS Count
            FROM {DbConstants.EventTable}
            WHERE {DbConstants.TimestampColumn} >= @Threshold
            GROUP BY {DbConstants.EventTypeColumn};";

        using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Threshold = timeThreshold }, cancellationToken: cancellationToken));

        var totalEvents     = await multi.ReadFirstAsync<int>();
        var eventTypeCounts = await multi.ReadAsync<(string EventType, int Count)>();

        var eventsByType      = eventTypeCounts.ToDictionary(x => x.EventType, x => x.Count);
        var successEvents     = eventsByType.TryGetValue("success", out var successCount) ? successCount : 0;
        var recentSuccessRate = totalEvents > 0
            ? (int)Math.Round(successEvents * 100.0 / totalEvents)
            : 0;

        return new DashboardOverview(totalEvents, eventsByType, recentSuccessRate);
    }
}