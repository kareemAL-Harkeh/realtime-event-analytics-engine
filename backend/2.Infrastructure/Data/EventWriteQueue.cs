using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// High-performance thread-safe queue utilizing System.Threading.Channels.
/// Optimized for Multiple Writers (API threads) and a Single Reader (Background worker).
/// </summary>
public sealed class EventWriteQueue : IEventWriteQueue
{
    private readonly Channel<LogEventCommand> _channel = Channel.CreateUnbounded<LogEventCommand>(new UnboundedChannelOptions
    {
        SingleReader = true,  // Guaranteed single background consumer
        SingleWriter = false, // Multiple API endpoints can push concurrently
        AllowSynchronousContinuations = false
    });

    public void Enqueue(LogEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        // TryWrite is non-allocating and thread-safe for unbounded channels
        _channel.Writer.TryWrite(command);
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.WaitToReadAsync(cancellationToken);
    }

    public bool TryDequeue(out LogEventCommand? command)
    {
        return _channel.Reader.TryRead(out command);
    }

    public async IAsyncEnumerable<LogEventCommand> DequeueAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Continuously reads data as long as the channel is open and data is arriving
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var command))
            {
                yield return command;
            }
        }
    }
}