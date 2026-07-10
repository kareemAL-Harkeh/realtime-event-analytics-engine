using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Core.Commands;

/// <summary>
/// High-throughput CQRS command handler decoupled from persistent network locking.
/// </summary>
public sealed class LogEventCommandHandler
{
    private readonly IEventWriteQueue _queue;

    public LogEventCommandHandler(IEventWriteQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    /// <summary>
    /// Processes incoming telemetry by validating, enriching time signatures, 
    /// and safely pushing to the high-speed memory channel buffer.
    /// </summary>
    public Task HandleAsync(LogEventCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        // Enrich the incoming command with precise Universal Time Coordinate if default
        var enriched = command with 
        { 
            Timestamp = command.Timestamp == default 
                ? DateTimeOffset.UtcNow 
                : command.Timestamp.ToUniversalTime() 
        };

        // API thread only drops the payload in the fast Channel buffer 
        // and returns immediately! No network blocking via awaiting Redis on the HTTP thread.
        _queue.Enqueue(enriched);

        return Task.CompletedTask;
    }
}