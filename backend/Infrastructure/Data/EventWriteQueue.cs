using System.Runtime.CompilerServices;
using System.Threading.Channels;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;

namespace RealTimeEventAnalyticsEngine.Infrastructure.Data;

/// <summary>
/// High-performance in-memory buffer backed by System.Threading.Channels.
/// 
/// - Multiple writers (API threads) are allowed.
/// - Single reader (the background service) is expected.
/// - Bounded capacity applies back-pressure when the system is overloaded.
/// </summary>
public sealed class EventWriteQueue : IEventWriteQueue
{
    // 10_000 is a reasonable default. Tune according to your expected load and memory budget.
    private const int Capacity = 10_000;

    private readonly Channel<LogEventCommand> _channel = Channel.CreateBounded<LogEventCommand>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,   // or DropWrite if you prefer dropping under pressure
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    /// <inheritdoc />
    public bool TryEnqueue(LogEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _channel.Writer.TryWrite(command);
    }

    /// <inheritdoc />
    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.WaitToReadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public bool TryDequeue(out LogEventCommand? command)
    {
        return _channel.Reader.TryRead(out command);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LogEventCommand> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (_channel.Reader.TryRead(out var command))
            {
                yield return command;
            }
        }
    }
}