using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Presentation.Hubs;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Background worker that drains the write queue, persists events in batches,
/// updates Redis, and broadcasts new events over SignalR.
///
/// A flush is triggered by whichever happens first:
/// 1) the batch reaches <see cref="BatchSize"/> events, or
/// 2) the periodic timer ticks (<see cref="BatchFlushInterval"/>).
///
/// The timer runs as its own independent loop rather than being checked inline
/// while reading the channel. If it were checked inline, a partially-filled batch
/// would only ever get flushed the next time an event happens to arrive - if traffic
/// goes quiet, the batch (and the "real-time" dashboard) would just stall.
/// </summary>
public sealed class EventWriteBackgroundService : BackgroundService
{
    private readonly IEventWriteQueue _queue;
    private readonly IEventRepository _repository;
    private readonly IRedisCacheService _cache;
    private readonly IHubContext<EventHub> _hubContext;
    private readonly ILogger<EventWriteBackgroundService> _logger;

    // Caps how many side-effect jobs (Redis + SignalR, one per event) can run
    // concurrently. Without this, a traffic spike spins up an unbounded number
    // of fire-and-forget tasks - the DB/Redis/SignalR connections would feel that
    // long before they'd naturally back-pressure on their own.
    private readonly SemaphoreSlim _sideEffectThrottle = new(initialCount: 200, maxCount: 200);

    private const int BatchSize = 100;
    private static readonly TimeSpan BatchFlushInterval = TimeSpan.FromSeconds(2);

    public EventWriteBackgroundService(
        IEventWriteQueue queue,
        IEventRepository repository,
        IRedisCacheService cache,
        IHubContext<EventHub> hubContext,
        ILogger<EventWriteBackgroundService> logger)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventWriteBackgroundService started.");

        var batch = new List<LogEventCommand>(BatchSize);

        // Guards `batch` since two independent loops touch it: the channel reader
        // below, and the periodic-flush loop running alongside it.
        using var batchLock = new SemaphoreSlim(1, 1);

        var periodicFlushLoop = RunPeriodicFlushAsync(batch, batchLock, stoppingToken);

        try
        {
            // Deliberately kept as the ONLY reader of the channel (it's configured
            // with SingleReader = true). The periodic flush loop never reads from
            // the queue - it only flushes whatever the reader has already collected.
            await foreach (var command in _queue.ReadAllAsync(stoppingToken))
            {
                var shouldFlush = false;

                await batchLock.WaitAsync(stoppingToken).ConfigureAwait(false);
                try
                {
                    batch.Add(command);
                    shouldFlush = batch.Count >= BatchSize;
                }
                finally
                {
                    batchLock.Release();
                }

                if (shouldFlush)
                    await FlushBatchAsync(batch, batchLock, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("EventWriteBackgroundService is shutting down.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in EventWriteBackgroundService.");
            throw;
        }
        finally
        {
            // Drain whatever is left so a restart/redeploy doesn't silently drop events.
            await FlushBatchAsync(batch, batchLock, CancellationToken.None).ConfigureAwait(false);
            await periodicFlushLoop.ConfigureAwait(false);
        }
    }

    private async Task RunPeriodicFlushAsync(
        List<LogEventCommand> batch,
        SemaphoreSlim batchLock,
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(BatchFlushInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await FlushBatchAsync(batch, batchLock, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown - the main loop's finally block does one last flush.
        }
    }

    private async Task FlushBatchAsync(
        List<LogEventCommand> batch,
        SemaphoreSlim batchLock,
        CancellationToken cancellationToken)
    {
        List<LogEventCommand> snapshot;

        // Always allowed to acquire the lock, even during shutdown (CancellationToken.None),
        // otherwise a cancelled token could stop us from flushing the final batch.
        await batchLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (batch.Count == 0) return;

            snapshot = new List<LogEventCommand>(batch);
            batch.Clear();
        }
        finally
        {
            batchLock.Release();
        }

        try
        {
            await _repository.SaveEventsBatchAsync(snapshot, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Persisted batch of {Count} events.", snapshot.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist batch of {Count} events. These events are lost " +
                "unless a retry/dead-letter mechanism is added.", snapshot.Count);

            // Deliberately return here: we do NOT want to cache/broadcast events that
            // never actually made it into Postgres. Previously side effects fired
            // as soon as an event was read off the queue, regardless of whether the
            // later DB write succeeded - meaning the dashboard could show events that
            // didn't exist in the database. Side effects now only run after a
            // confirmed successful write.
            return;
        }

        foreach (var command in snapshot)
        {
            _ = ProcessSideEffectsAsync(command, cancellationToken);
        }
    }

    private async Task ProcessSideEffectsAsync(LogEventCommand command, CancellationToken cancellationToken)
    {
        await _sideEffectThrottle.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var cacheTask = _cache.CacheEventAsync(command, cancellationToken);

            var broadcastTask = _hubContext.Clients.All.SendAsync(
                "ReceiveEvent",
                new
                {
                    eventId = command.EventId,
                    eventType = command.EventType,
                    source = command.Source,
                    timestamp = command.Timestamp,
                    payload = command.Payload
                },
                cancellationToken);

            await Task.WhenAll(cacheTask, broadcastTask).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Side-effect failed for event type {EventType}", command.EventType);
        }
        finally
        {
            _sideEffectThrottle.Release();
        }
    }

    public override void Dispose()
    {
        _sideEffectThrottle.Dispose();
        base.Dispose();
    }
}
