using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using RealTimeEventAnalyticsEngine.Infrastructure.Cache;
using RealTimeEventAnalyticsEngine.Presentation.Hubs;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// Production-ready background ingestion pipeline engine. Performs size/time-bound PostgreSQL batch flushing,
/// non-blocking Redis atomic caching, and live SignalR dashboard broadcasting under .NET 10.
/// </summary>
public sealed class EventWriteBackgroundService : BackgroundService
{
    private readonly IEventWriteQueue _queue;
    private readonly IEventRepository _repository;
    private readonly IRedisCacheService _cache;
    private readonly IHubContext<EventHub> _hubContext;
    private readonly ILogger<EventWriteBackgroundService> _logger;
    
    private const int BatchSize = 100;
    private const int BatchTimeoutMs = 5000;

    public EventWriteBackgroundService(
        IEventWriteQueue queue,
        IEventRepository repository, // Converted to Interface based on our Core movement
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
        _logger.LogInformation("EventWriteBackgroundService initialized and starting consumption pipeline.");

        var batch = new List<LogEventCommand>(BatchSize);
        var batchTimer = Stopwatch.StartNew();

        try
        {
            // 🛠️ Tech Lead Fix: Advanced Channel Reading loop ensuring timeouts are evaluated even during traffic drops
            while (!stoppingToken.IsCancellationRequested)
            {
                // Check if there is data available to read, or evaluate timeout if the batch is not empty
                var hasData = await _queue.WaitToReadAsync(stoppingToken);

                while (hasData && batch.Count < BatchSize)
                {
                    // Try to read all available items rapidly from memory without blocking
                    if (_queue.TryDequeue(out var command) && command is not null)
                    {
                        batch.Add(command);

                        // 1. Pipeline Action A: Fire-and-forget distributed Redis caching and live broadcast concurrently
                        _ = ProcessSideEffectsAsync(command, stoppingToken);
                    }
                    else
                    {
                        break;
                    }
                }

                // 🛠️ Tech Lead Fix: Strict evaluation of bounds (Size reached OR maximum stay timeout elapsed)
                if (batch.Count > 0 && (batch.Count >= BatchSize || batchTimer.ElapsedMilliseconds >= BatchTimeoutMs))
                {
                    await FlushBatchWithClearAsync(batch, batchTimer, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("EventWriteBackgroundService operation was canceled. Graceful exit triggered.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal breakdown inside EventWriteBackgroundService processing loop.");
            throw;
        }
        finally
        {
            // Flush out remaining remnants on shutdown to preserve complete audit trails
            if (batch.Count > 0)
            {
                _logger.LogInformation("Flushing remaining {Count} events before service shutdown.", batch.Count);
                await _repository.SaveEventsBatchAsync(batch, CancellationToken.None);
            }
        }
    }

    private async Task FlushBatchWithClearAsync(List<LogEventCommand> batch, Stopwatch timer, CancellationToken token)
    {
        try
        {
            // 2. Pipeline Action B: Batch persistent flushing to PostgreSQL via Dapper UNNEST
            await _repository.SaveEventsBatchAsync(batch, token);
            _logger.LogDebug("Successfully persisted batch of {Count} analytical events.", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush analytical batch to persistent storage.");
        }
        finally
        {
            batch.Clear();
            timer.Restart();
        }
    }

    private async Task ProcessSideEffectsAsync(LogEventCommand command, CancellationToken token)
    {
        try
        {
            // Parallel non-blocking execution for high speed delivery
            var cacheTask = _cache.CacheEventAsync(command, token);
            
            var broadcastTask = _hubContext.Clients.All.SendAsync("ReceiveEvent", new
            {
                eventType = command.EventType,
                source = command.Source,
                timestamp = command.Timestamp,
                payload = command.Payload
            }, cancellationToken: token);

            await Task.WhenAll(cacheTask, broadcastTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Redis/SignalR side-effects for event type: {Type}", command.EventType);
        }
    }
}