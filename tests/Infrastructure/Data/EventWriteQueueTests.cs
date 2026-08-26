using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Infrastructure.Data;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Infrastructure.Data;

/// <summary>
/// Covers EventWriteQueue - specifically the back-pressure contract the whole
/// ingestion path depends on. EventsEndpoints returns 503 the moment
/// TryEnqueue returns false, so "does a full queue actually return false
/// instead of blocking" is load-bearing behavior for this API, not an
/// implementation detail worth skipping.
/// </summary>
public sealed class EventWriteQueueTests
{
    private static LogEventCommand SampleCommand(string eventType = "info") => new()
    {
        EventType = eventType,
        Payload = "{}",
        Source = "test-service",
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public void TryEnqueue_BelowCapacity_ReturnsTrue()
    {
        var queue = new EventWriteQueue();

        var accepted = queue.TryEnqueue(SampleCommand());

        Assert.True(accepted);
    }

    [Fact]
    public void TryEnqueue_NullCommand_Throws()
    {
        var queue = new EventWriteQueue();

        Assert.Throws<ArgumentNullException>(() => queue.TryEnqueue(null!));
    }

    [Fact]
    public async Task ReadAllAsync_YieldsEnqueuedCommandsInOrder()
    {
        var queue = new EventWriteQueue();
        var first = SampleCommand("first");
        var second = SampleCommand("second");

        queue.TryEnqueue(first);
        queue.TryEnqueue(second);

        using var cts = new CancellationTokenSource();
        var received = new List<LogEventCommand>();

        await foreach (var command in queue.ReadAllAsync(cts.Token))
        {
            received.Add(command);
            if (received.Count == 2)
            {
                break;
            }
        }

        Assert.Equal([first, second], received);
    }

    [Fact]
    public void TryDequeue_EmptyQueue_ReturnsFalse()
    {
        var queue = new EventWriteQueue();

        var dequeued = queue.TryDequeue(out var command);

        Assert.False(dequeued);
        Assert.Null(command);
    }

    [Fact]
    public void TryEnqueue_AtCapacity_ReturnsFalse()
    {
        // EventWriteQueue's capacity is a private const (10_000) - intentionally
        // not exposed publicly, so this test fills it rather than reaching in
        // via reflection to read it. Slower than the other tests here but
        // still fast in absolute terms (in-memory only, no I/O), and it's the
        // only honest way to verify the back-pressure contract EventsEndpoints
        // actually relies on for its 503 response.
        var queue = new EventWriteQueue();

        const int capacity = 10_000;
        for (var i = 0; i < capacity; i++)
        {
            Assert.True(queue.TryEnqueue(SampleCommand()));
        }

        var rejected = queue.TryEnqueue(SampleCommand());

        Assert.False(rejected);
    }
}
