using System.Runtime.CompilerServices;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Core.Interfaces;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Core.Commands;

/// <summary>
/// Covers LogEventCommandHandler - specifically the Timestamp enrichment logic
/// that the LogEventCommandValidator fix (see LogEventCommandValidatorTests)
/// depends on actually running afterward: the validator lets `default` through
/// on the assumption the handler will fix it up. This is where that assumption
/// gets verified.
///
/// A hand-rolled fake is used instead of a mocking library on purpose:
/// IEventWriteQueue is small and stable, and a real (if trivial) fake
/// implementation is easier to trust at a glance than a mock's recorded
/// setup - same reasoning behind favoring Dapper over EF and the built-in
/// rate limiter over a third-party package elsewhere in this project. Reach
/// for the plain, direct tool when the direct tool is this simple.
/// </summary>
public sealed class LogEventCommandHandlerTests
{
    private sealed class FakeEventWriteQueue : IEventWriteQueue
    {
        public LogEventCommand? LastEnqueued { get; private set; }
        public bool AcceptNext { get; set; } = true;

        public bool TryEnqueue(LogEventCommand command)
        {
            LastEnqueued = command;
            return AcceptNext;
        }

        public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(true);

        public bool TryDequeue(out LogEventCommand? command)
        {
            command = null;
            return false;
        }

        public async IAsyncEnumerable<LogEventCommand> ReadAllAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private static LogEventCommand CommandWithTimestamp(DateTimeOffset timestamp) => new()
    {
        EventType = "info",
        Payload = "{}",
        Source = "order-service",
        Timestamp = timestamp
    };

    [Fact]
    public void Handle_MissingTimestamp_EnrichesToUtcNow()
    {
        var queue = new FakeEventWriteQueue();
        var handler = new LogEventCommandHandler(queue);
        var before = DateTimeOffset.UtcNow;

        handler.Handle(CommandWithTimestamp(default));

        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(queue.LastEnqueued);
        Assert.InRange(queue.LastEnqueued!.Timestamp, before, after);
    }

    [Fact]
    public void Handle_ExplicitTimestamp_IsNormalizedToUtcNotReplaced()
    {
        var queue = new FakeEventWriteQueue();
        var handler = new LogEventCommandHandler(queue);

        // A non-UTC offset that should be converted, not overwritten with "now".
        var explicitTimestamp = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(3));

        handler.Handle(CommandWithTimestamp(explicitTimestamp));

        Assert.Equal(explicitTimestamp.ToUniversalTime(), queue.LastEnqueued!.Timestamp);
    }

    [Fact]
    public void Handle_QueueRejects_ReturnsFalse()
    {
        var queue = new FakeEventWriteQueue { AcceptNext = false };
        var handler = new LogEventCommandHandler(queue);

        var accepted = handler.Handle(CommandWithTimestamp(DateTimeOffset.UtcNow));

        Assert.False(accepted);
    }

    [Fact]
    public void Handle_NullCommand_Throws()
    {
        var handler = new LogEventCommandHandler(new FakeEventWriteQueue());

        Assert.Throws<ArgumentNullException>(() => handler.Handle(null!));
    }

    [Fact]
    public async Task HandleAsync_CancelledToken_Throws()
    {
        var handler = new LogEventCommandHandler(new FakeEventWriteQueue());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.HandleAsync(CommandWithTimestamp(DateTimeOffset.UtcNow), cts.Token));
    }
}
