using Pigeon.Messaging.Outbox;

namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Tests;

public class EntityFrameworkLegacyOutboxStorageTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Constructor_RejectsNullFactory()
        => Assert.Throws<ArgumentNullException>(() => new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(null));

    [Fact]
    public async Task AddAndSave_PersistsMessage()
    {
        using var fixture = new LegacyOutboxFixture();
        await using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();
        using var db = fixture.CreateDbContext();
        Assert.Equal(message.Id, Assert.Single(db.Set<OutboxMessage>()).Id);
    }

    [Fact]
    public async Task LockPending_FiltersByStatusAndDueTime()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var due = NewMessage();
        var future = NewMessage();
        future.NextAttemptOnUtc = _now.AddMinutes(1);
        var failed = NewMessage();
        failed.Status = OutboxMessageStatus.Failed;
        await storage.AddAsync(due);
        await storage.AddAsync(future);
        await storage.AddAsync(failed);
        await storage.SaveChangesAsync();

        var messages = await storage.LockPendingAsync(10, TimeSpan.FromMinutes(1), _now);
        Assert.Equal(due.Id, Assert.Single(messages).Id);
    }

    [Fact]
    public async Task LockPending_IncludesExpiredLock()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        message.Status = OutboxMessageStatus.Locked;
        message.LockedOnUtc = _now.AddMinutes(-1);
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();

        var messages = await storage.LockPendingAsync(10, TimeSpan.FromMinutes(1), _now);

        Assert.Equal(message.Id, Assert.Single(messages).Id);
    }

    [Fact]
    public async Task LockPending_ExcludesActiveLock()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        message.Status = OutboxMessageStatus.Locked;
        message.LockedOnUtc = _now;
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();

        Assert.Empty(await storage.LockPendingAsync(10, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public async Task Lock_AllowsRetryDueAtCurrentTime()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        message.NextAttemptOnUtc = _now;
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();

        Assert.NotNull(await storage.LockAsync(message.Id, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public async Task Lock_ReturnsNullForMissingOrNotDueMessages()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        message.NextAttemptOnUtc = _now.AddMinutes(1);
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();

        Assert.Null(await storage.LockAsync(Guid.NewGuid(), TimeSpan.FromMinutes(1), _now));
        Assert.Null(await storage.LockAsync(message.Id, TimeSpan.FromMinutes(1), _now));
    }

    [Fact]
    public async Task Lock_AllowsExpiredButNotActiveLock()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();

        Assert.NotNull(await storage.LockAsync(message.Id, TimeSpan.FromMinutes(1), _now));
        Assert.Null(await storage.LockAsync(message.Id, TimeSpan.FromMinutes(1), _now.AddSeconds(10)));
        Assert.NotNull(await storage.LockAsync(message.Id, TimeSpan.FromMinutes(1), _now.AddMinutes(1)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkFailed_SetsRetryOrFailure(bool retry)
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();

        await storage.MarkFailedAsync(message.Id, "failure", _now, retry ? _now.AddMinutes(1) : null);
        Assert.Equal(retry ? OutboxMessageStatus.Pending : OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("failure", message.LastError);
    }

    [Fact]
    public async Task MarkPublished_ClearsFailureAndCleanRemovesMessage()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        var message = NewMessage();
        await storage.AddAsync(message);
        await storage.SaveChangesAsync();
        await storage.MarkFailedAsync(message.Id, "failure", _now, _now);
        await storage.MarkPublishedAsync(message.Id, _now);
        await storage.SaveChangesAsync();

        Assert.Null(message.LastError);
        Assert.Null(message.NextAttemptOnUtc);
        Assert.Equal(0, await storage.CleanPublishedAsync(_now.AddSeconds(-1), 10));
        Assert.Equal(1, await storage.CleanPublishedAsync(_now, 10));
        await storage.SaveChangesAsync();
        using var db = fixture.CreateDbContext();
        Assert.Empty(db.Set<OutboxMessage>());
    }

    [Fact]
    public async Task MarkPublished_RejectsMissingMessage()
    {
        using var fixture = new LegacyOutboxFixture();
        using var storage = new EntityFrameworkOutboxStorage<LegacyOutboxDbContext>(fixture);
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.MarkPublishedAsync(Guid.NewGuid(), _now));
    }

    private OutboxMessage NewMessage() => new() { CreatedOnUtc = _now, Payload = "{}", PayloadType = "test" };
}
