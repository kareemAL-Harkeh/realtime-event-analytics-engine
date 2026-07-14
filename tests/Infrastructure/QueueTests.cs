using FluentAssertions;
using RealTimeEventAnalyticsEngine.Core.Commands;
using RealTimeEventAnalyticsEngine.Infrastructure.Data;
using Xunit;

namespace RealTimeEventAnalyticsEngine.Tests.Infrastructure;

/// <summary>
/// Infrastructure-level integration tests verifying the integrity of the high-speed memory buffer.
/// </summary>
public class QueueTests
{
    [Fact]
    public async Task EventWriteQueue_ShouldEnqueueAndDequeueSingleItem_Successfully()
    {
        // Arrange: Prepare high-density telemetry workload
        var queue = new EventWriteQueue();
        var expectedCommand = new LogEventCommand(
            EventType: "info", 
            Payload: "{\"message\":\"ok\"}", 
            Source: "service-a", 
            Timestamp: DateTimeOffset.UtcNow
        );

        // Act: Push object into the thread-safe channel pipeline
        queue.Enqueue(expectedCommand);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var dataAvailable = await queue.WaitToReadAsync(cts.Token);

        // Assert
        dataAvailable.Should().BeTrue("because data was just enqueued into the channel buffer");
        
        var dequeueSuccess = queue.TryDequeue(out var actualCommand);
        
        dequeueSuccess.Should().BeTrue();
        actualCommand.Should().NotBeNull();
        
        // 🔥 Ultra Fix: Compare the entire record structure structural integrity in 1 single line!
        actualCommand.Should().BeEquivalentTo(expectedCommand, options => options.ComparingByMembers<LogEventCommand>());
    }
}