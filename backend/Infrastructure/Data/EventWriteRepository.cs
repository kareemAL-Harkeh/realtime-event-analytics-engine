using System.Data;
using System.Data.Common;
using Bogus;
using Dapper;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Core.Queries;
using RealTimeEventAnalyticsEngine.Infrastructure.Constants;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Dapper-based repository optimized for high-speed batch inserts and analytical queries.
/// </summary>
public sealed class EventWriteRepository : IEventRepository
{
    private readonly IEventAnalyticsDbContext _dbContext;
    private readonly ILogger<EventWriteRepository> _logger;

    private static readonly string[] EventTypes = ["success", "error", "warning", "info", "critical"];
    private static readonly string[] Sources =
    [
        "order-service", "payment-service", "analytics-service",
        "identity-server", "monitoring-agent", "notification-service",
        "inventory-service", "gateway-proxy"
    ];

    public EventWriteRepository(IEventAnalyticsDbContext dbContext, ILogger<EventWriteRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EnsureEventTableExistsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var sql = $@"
            CREATE TABLE IF NOT EXISTS {DbConstants.EventTable} (
                {DbConstants.EventIdColumn}   UUID        PRIMARY KEY,
                {DbConstants.EventTypeColumn} TEXT        NOT NULL,
                {DbConstants.TimestampColumn} TIMESTAMPTZ NOT NULL,
                {DbConstants.PayloadColumn}   TEXT,
                {DbConstants.SourceColumn}    TEXT        NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_events_timestamp 
                ON {DbConstants.EventTable} ({DbConstants.TimestampColumn} DESC);

            CREATE INDEX IF NOT EXISTS ix_events_type_timestamp 
                ON {DbConstants.EventTable} ({DbConstants.EventTypeColumn}, {DbConstants.TimestampColumn} DESC);";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<bool> HasAnyEventsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var sql = $"SELECT EXISTS (SELECT 1 FROM {DbConstants.EventTable} LIMIT 1);";
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task SeedSampleEventsAsync(CancellationToken cancellationToken = default)
    {
        if (await HasAnyEventsAsync(cancellationToken))
            return;

        var faker = new Faker();

        var events = Enumerable.Range(0, 1000).Select(_ =>
        {
            var eventType = faker.PickRandom(EventTypes);
            var source = faker.PickRandom(Sources);

            var payload = eventType switch
            {
                "error" => $"{{\"message\":\"{faker.System.Exception().Message}\",\"code\":{faker.Random.Int(400, 503)}}}",
                "warning" => $"{{\"message\":\"{faker.Lorem.Sentence()}\",\"threshold\":{faker.Random.Int(75, 95)}}}",
                "critical" => $"{{\"message\":\"Critical failure in {faker.System.FileName()}\",\"severity\":\"HIGH\"}}",
                "info" => $"{{\"message\":\"{faker.Lorem.Sentence()}\"}}",
                _ => $"{{\"message\":\"{faker.Hacker.Phrase()}\"}}"
            };

            return new LogEventCommand
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                Payload = payload,
                Source = source,
                Timestamp = faker.Date.BetweenOffset(
                    DateTimeOffset.UtcNow.AddDays(-30),
                    DateTimeOffset.UtcNow)
            };
        }).ToList();

        await SaveEventsBatchAsync(events, cancellationToken);
    }

    public async Task SaveEventsBatchAsync(
        IReadOnlyList<LogEventCommand> commands,
        CancellationToken cancellationToken = default)
    {
        if (commands is null || commands.Count == 0)
            return;

        await using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);
        await connection.OpenAsync(cancellationToken);

        // ON CONFLICT DO NOTHING matters here: EventId is the primary key, and this
        // is a single batched INSERT built from UNNEST arrays. Without it, ONE
        // duplicate/retried EventId anywhere in the batch throws a unique-violation
        // and fails the entire statement - taking down every other (perfectly valid)
        // event in the same batch with it. With it, the duplicate row is silently
        // skipped and the rest of the batch persists normally.
        var sql = $@"
            INSERT INTO {DbConstants.EventTable}
            ({DbConstants.EventIdColumn}, {DbConstants.EventTypeColumn}, {DbConstants.TimestampColumn}, {DbConstants.PayloadColumn}, {DbConstants.SourceColumn})
            SELECT * FROM UNNEST(@Ids, @EventTypes, @Timestamps, @Payloads, @Sources)
            ON CONFLICT ({DbConstants.EventIdColumn}) DO NOTHING;";

        var count = commands.Count;
        var ids = new Guid[count];
        var eventTypes = new string[count];
        var timestamps = new DateTimeOffset[count];
        var payloads = new string[count];
        var sources = new string[count];

        for (var i = 0; i < count; i++)
        {
            var cmd = commands[i];

            // Prefer the EventId coming from the client if it is a valid GUID, otherwise generate one.
            if (Guid.TryParse(cmd.EventId, out var parsedId))
            {
                ids[i] = parsedId;
            }
            else
            {
                ids[i] = Guid.NewGuid();

                // Only worth logging if the client actually sent something - an
                // omitted EventId is expected and handled silently, but a client
                // that sent a non-GUID value probably expected idempotency and
                // is now silently not getting it.
                if (!string.IsNullOrEmpty(cmd.EventId))
                {
                    _logger.LogWarning(
                        "EventId '{EventId}' from source {Source} is not a valid GUID; generated {GeneratedId} instead.",
                        cmd.EventId, cmd.Source, ids[i]);
                }
            }

            eventTypes[i] = cmd.EventType;
            timestamps[i] = cmd.Timestamp;
            payloads[i] = cmd.Payload;
            sources[i] = cmd.Source;
        }

        await connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Ids = ids,
            EventTypes = eventTypes,
            Timestamps = timestamps,
            Payloads = payloads,
            Sources = sources
        }, cancellationToken: cancellationToken));
    }

    public async Task<DashboardOverview> GetDashboardOverviewAsync(
        FetchDashboardDataQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var connection = await _dbContext.CreateConnectionAsync(cancellationToken);

        var threshold = DateTimeOffset.UtcNow.AddMinutes(-query.WindowMinutes);

        var sql = $@"
            SELECT COUNT(1)
            FROM {DbConstants.EventTable}
            WHERE {DbConstants.TimestampColumn} >= @Threshold;

            SELECT {DbConstants.EventTypeColumn} AS EventType, COUNT(1) AS Count
            FROM {DbConstants.EventTable}
            WHERE {DbConstants.TimestampColumn} >= @Threshold
            GROUP BY {DbConstants.EventTypeColumn};";

        await using var multi = await connection.QueryMultipleAsync(
            new CommandDefinition(sql, new { Threshold = threshold }, cancellationToken: cancellationToken));

        var totalEvents = await multi.ReadFirstAsync<int>();
        var typeCounts = (await multi.ReadAsync<(string EventType, int Count)>())
            .ToDictionary(x => x.EventType, x => x.Count);

        var successCount = typeCounts.GetValueOrDefault("success");
        var successRate = totalEvents > 0
            ? (int)Math.Round(successCount * 100.0 / totalEvents)
            : 0;

        return new DashboardOverview(totalEvents, typeCounts, successRate);
    }
}
