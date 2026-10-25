namespace Pigeon.Messaging.Tests.Outbox;

using Mule.Diagnostics;
using NSubstitute;
using Pigeon.Messaging.Outbox;

public class MuleOutboxDiagnosticsTests
{
    [Fact]
    public void Constructor_RequiresDiagnostics()
        => Assert.Throws<ArgumentNullException>(() => new MuleOutboxDiagnostics(null));

    [Fact]
    public async Task GetSnapshot_MapsMuleMetrics()
    {
        var source = Substitute.For<IMuleDiagnostics>();
        var timestamp = DateTimeOffset.UtcNow;
        var lanes = new Dictionary<string, int> { ["critical"] = 7 };
        source.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(new MuleDiagnosticsSnapshot
        {
            Pending = 1, Locked = 2, Completed = 3, Failed = 4, RuntimeCompleted = 5, RuntimeFailed = 6,
            ThroughputPerMinute = 7, OldestPendingOnUtc = timestamp, OldestFailedOnUtc = timestamp,
            AverageEnqueueToExecutionLatency = TimeSpan.FromSeconds(1), AverageExecutionLatency = TimeSpan.FromSeconds(2),
            BacklogByLane = lanes, CompletedPerMinuteByLane = lanes, RuntimeFailedByLane = lanes
        });
        using var cancellation = new CancellationTokenSource();
        var result = await new MuleOutboxDiagnostics(source).GetSnapshotAsync(cancellation.Token);
        Assert.Equal((1, 2, 3, 4, 5, 6, 7), (result.PendingMessages, result.LockedMessages, result.PublishedMessages,
            result.FailedMessages, result.RuntimeCompletedMessages, result.RuntimeFailedMessages, result.ThroughputPerMinute));
        Assert.Equal(timestamp, result.OldestPendingMessageOnUtc);
        Assert.Equal(timestamp, result.OldestFailedMessageOnUtc);
        Assert.Equal(TimeSpan.FromSeconds(1), result.AverageEnqueueToDispatchLatency);
        Assert.Equal(TimeSpan.FromSeconds(2), result.AverageDispatchToPublishedLatency);
        Assert.Same(lanes, result.BacklogByLane);
        Assert.Same(lanes, result.CompletedPerMinuteByLane);
        Assert.Same(lanes, result.RuntimeFailedByLane);
        await source.Received(1).GetSnapshotAsync(cancellation.Token);
    }
}
