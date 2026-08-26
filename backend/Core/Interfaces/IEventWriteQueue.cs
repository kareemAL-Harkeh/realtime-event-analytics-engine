using RealTimeEventAnalyticsEngine.Core.Commands;

namespace RealTimeEventAnalyticsEngine.Core.Interfaces;

/// <summary>
/// High-throughput, thread-safe buffer that sits between the API and the background writer.
/// 
/// Implementation is expected to be backed by System.Threading.Channels.Channel&lt;T&gt;
/// (preferably a bounded channel to apply back-pressure when the system is overloaded).
/// </summary>
public interface IEventWriteQueue
{
    /// <summary>
    /// Attempts to enqueue an event without blocking.
    /// Returns false if the channel is full (when using a bounded channel).
    /// </summary>
    bool TryEnqueue(LogEventCommand command);

    /// <summary>
    /// Asynchronously waits until data is available to read, or the token is cancelled.
    /// </summary>
    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to read a single item from the queue without waiting.
    /// </summary>
    bool TryDequeue(out LogEventCommand? command);

    /// <summary>
    /// Continuously yields all available items as they arrive.
    /// Ideal for the background service that drains the queue.
    /// </summary>
    IAsyncEnumerable<LogEventCommand> ReadAllAsync(CancellationToken cancellationToken = default);
}