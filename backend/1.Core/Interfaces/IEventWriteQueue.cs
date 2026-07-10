using RealTimeEventAnalyticsEngine.Core.Commands;

namespace RealTimeEventAnalyticsEngine.Core.Interfaces;

/// <summary>
/// Provides a thread-safe, high-throughput memory channel acting as a buffer for incoming telemetries.
/// </summary>
public interface IEventWriteQueue
{
    /// <summary>
    /// Enqueues an event command into the channel buffer. Non-blocking.
    /// </summary>
    void Enqueue(LogEventCommand command);

    /// <summary>
    /// Waits until data is available to read or the cancellation token is triggered.
    /// </summary>
    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to dequeue a single item from the queue.
    /// </summary>
    bool TryDequeue(out LogEventCommand? command);

    /// <summary>
    /// Streams all available items from the queue asynchronously.
    /// </summary>
    IAsyncEnumerable<LogEventCommand> DequeueAllAsync(CancellationToken cancellationToken);
}