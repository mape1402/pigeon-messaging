using Pigeon.Messaging.Outbox;

namespace Pigeon.Messaging.Outbox.InMemory.Tests;

public class InMemoryOutboxStoreTests
{
    private readonly InMemoryOutboxStore _store = new();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Messages_ReturnsCopiesSortedByCreation()
    {
        var later = Message(_now);
        var earlier = Message(_now.AddSeconds(-1));
        _store.Add(later);
        _store.Add(earlier);
        later.Topic = "modified";

        var messages = _store.Messages.ToArray();

        Assert.Equal(new[] { earlier.Id, later.Id }, messages.Select(x => x.Id));
        Assert.Equal("orders", messages[1].Topic);
        messages[0].Topic = "changed";
        Assert.Equal("orders", _store.Messages.First().Topic);
    }

    [Fact]
    public void GetPending_RespectsDueTimeAndBatchSize()
    {
        var due = Message(_now.AddMinutes(-2));
        var future = Message(_now.AddMinutes(-1));
        future.NextAttemptOnUtc = _now.AddSeconds(1);
        _store.Add(due);
        _store.Add(future);

        Assert.Equal(due.Id, Assert.Single(_store.GetPending(1, TimeSpan.FromMinutes(1), _now)).Id);
        Assert.Empty(_store.GetPending(0, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public void GetPending_IncludesRetryDueAtCurrentTime()
    {
        var message = Message(_now);
        message.NextAttemptOnUtc = _now;
        _store.Add(message);

        Assert.Equal(message.Id, Assert.Single(_store.GetPending(10, TimeSpan.FromMinutes(1), _now)).Id);
    }

    [Fact]
    public void GetPending_ExcludesFailedMessages()
    {
        var message = Message(_now);
        message.Status = OutboxMessageStatus.Failed;
        _store.Add(message);

        Assert.Empty(_store.GetPending(10, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public void Lock_AllowsRetryDueAtCurrentTime()
    {
        var message = Message(_now);
        message.NextAttemptOnUtc = _now;
        _store.Add(message);

        Assert.NotNull(_store.Lock(message.Id, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public void CleanPublished_LeavesRecentlyPublishedMessages()
    {
        var message = Message(_now);
        _store.Add(message);
        _store.MarkPublished(message.Id, _now);

        Assert.Equal(0, _store.CleanPublished(_now.AddSeconds(-1), 10));
        Assert.Single(_store.Messages);
    }

    [Fact]
    public void Lock_RejectsMissingAndFutureMessages()
    {
        var future = Message(_now);
        future.NextAttemptOnUtc = _now.AddMinutes(1);
        _store.Add(future);

        Assert.Null(_store.Lock(Guid.NewGuid(), TimeSpan.FromMinutes(1), _now));
        Assert.Null(_store.Lock(future.Id, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public void Lock_AllowsExpiredLocksButNotActiveLocks()
    {
        var message = Message(_now);
        _store.Add(message);

        Assert.NotNull(_store.Lock(message.Id, TimeSpan.FromMinutes(1), _now));
        Assert.Null(_store.Lock(message.Id, TimeSpan.FromMinutes(1), _now.AddSeconds(30)));
        Assert.Empty(_store.GetPending(10, TimeSpan.FromMinutes(1), _now.AddSeconds(30)));
        Assert.Single(_store.GetPending(10, TimeSpan.FromMinutes(1), _now.AddMinutes(1)));
        Assert.NotNull(_store.Lock(message.Id, TimeSpan.FromMinutes(1), _now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarkFailed_RecordsAttemptAndRetryState(bool retry)
    {
        var message = Message(_now);
        _store.Add(message);
        var next = retry ? _now.AddMinutes(1) : (DateTimeOffset?)null;

        _store.MarkFailed(message.Id, "failure", next);

        var saved = Assert.Single(_store.Messages);
        Assert.Equal(retry ? OutboxMessageStatus.Pending : OutboxMessageStatus.Failed, saved.Status);
        Assert.Equal(1, saved.Attempts);
        Assert.Equal("failure", saved.LastError);
        Assert.Equal(next, saved.NextAttemptOnUtc);
    }

    [Fact]
    public void MarkPublished_ClearsFailureAndRemovesOnlyEligibleRecords()
    {
        var published = Message(_now.AddMinutes(-1));
        var pending = Message(_now);
        _store.Add(published);
        _store.Add(pending);
        _store.MarkFailed(published.Id, "failure", _now);
        _store.MarkPublished(published.Id, _now);

        var saved = _store.Messages.Single(x => x.Id == published.Id);
        Assert.Equal(OutboxMessageStatus.Published, saved.Status);
        Assert.Null(saved.LastError);
        Assert.Null(saved.NextAttemptOnUtc);
        Assert.Equal(0, _store.CleanPublished(_now.AddSeconds(-1), 10));
        Assert.Equal(1, _store.CleanPublished(_now, 10));
        Assert.Equal(pending.Id, Assert.Single(_store.Messages).Id);
    }

    [Fact]
    public void MarkPublished_RejectsMissingMessage()
        => Assert.Throws<InvalidOperationException>(() => _store.MarkPublished(Guid.NewGuid(), _now));

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        _store.Add(Message(_now));
        _store.Clear();
        Assert.Empty(_store.Messages);
    }

    private static OutboxMessage Message(DateTimeOffset created) => new() { Topic = "orders", CreatedOnUtc = created };
}
