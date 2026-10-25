using NSubstitute;
using Pigeon.Messaging.Outbox;

namespace Pigeon.Messaging.Outbox.InMemory.Tests;

public class InMemoryOutboxDiagnosticsTests
{
    [Fact]
    public void Constructor_RejectsNullOutbox()
        => Assert.Throws<ArgumentNullException>(() => new InMemoryOutboxDiagnostics(null));

    [Fact]
    public async Task Snapshot_ReportsCountsAndOldestTimes()
    {
        var now = DateTimeOffset.UtcNow;
        var outbox = Substitute.For<IInMemoryOutbox>();
        outbox.Messages.Returns(new[]
        {
            new OutboxMessage { Status = OutboxMessageStatus.Pending, CreatedOnUtc = now.AddMinutes(-5) },
            new OutboxMessage { Status = OutboxMessageStatus.Locked, CreatedOnUtc = now.AddMinutes(-4) },
            new OutboxMessage { Status = OutboxMessageStatus.Published, CreatedOnUtc = now.AddMinutes(-3) },
            new OutboxMessage { Status = OutboxMessageStatus.Failed, CreatedOnUtc = now.AddMinutes(-2), LastError = "failure" }
        });

        var result = await new InMemoryOutboxDiagnostics(outbox).GetSnapshotAsync();

        Assert.Equal(1, result.PendingMessages);
        Assert.Equal(1, result.LockedMessages);
        Assert.Equal(1, result.PublishedMessages);
        Assert.Equal(1, result.FailedMessages);
        Assert.Equal(now.AddMinutes(-5), result.OldestPendingMessageOnUtc);
        Assert.Equal(now.AddMinutes(-2), result.OldestFailedMessageOnUtc);
        Assert.Equal("failure", result.LastFailure);
    }

    [Fact]
    public async Task Snapshot_UsesLatestFailureAndOldestFailedTime()
    {
        var now = DateTimeOffset.UtcNow;
        var outbox = Substitute.For<IInMemoryOutbox>();
        outbox.Messages.Returns(new[]
        {
            new OutboxMessage { Status = OutboxMessageStatus.Failed, CreatedOnUtc = now.AddMinutes(-2), LastError = "old" },
            new OutboxMessage { Status = OutboxMessageStatus.Failed, CreatedOnUtc = now.AddMinutes(-1), LastError = "new" }
        });
        var snapshot = await new InMemoryOutboxDiagnostics(outbox).GetSnapshotAsync();
        Assert.Equal("new", snapshot.LastFailure);
        Assert.Equal(now.AddMinutes(-2), snapshot.OldestFailedMessageOnUtc);
    }
}
