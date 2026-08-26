using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Core.Commands;

/// <summary>
/// Handles incoming LogEventCommand instances.
/// 
/// The main goal of this handler is to stay extremely fast and non-blocking.
/// It only enriches the command slightly and pushes it into the in-memory write queue.
/// Persistent storage and any heavy work happen later in the background service.
/// </summary>
public sealed class LogEventCommandHandler
{
    private readonly IEventWriteQueue _queue;

    public LogEventCommandHandler(IEventWriteQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>
    /// Enriches the command (mainly the timestamp) and enqueues it.
    /// Returns true when the event was successfully accepted into the queue.
    /// </summary>
    public bool Handle(LogEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Normalize timestamp to UTC. If the caller didn't provide one, use current UTC time.
        var enriched = command with
        {
            Timestamp = command.Timestamp == default
                ? DateTimeOffset.UtcNow
                : command.Timestamp.ToUniversalTime()
        };

        // Just drop it into the high-speed channel and return immediately.
        // No database calls, no Redis, no network on the request thread.
        return _queue.TryEnqueue(enriched);
    }

    /// <summary>
    /// Async version kept for compatibility with future MediatR or pipeline usage.
    /// Currently just wraps the synchronous fast path.
    /// </summary>
    public Task<bool> HandleAsync(LogEventCommand command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Handle(command));
    }
}