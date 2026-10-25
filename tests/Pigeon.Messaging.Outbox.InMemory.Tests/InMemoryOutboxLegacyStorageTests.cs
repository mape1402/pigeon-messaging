using System.Transactions;
using Pigeon.Messaging.Outbox;

namespace Pigeon.Messaging.Outbox.InMemory.Tests;

public class InMemoryOutboxLegacyStorageTests
{
    private readonly InMemoryOutboxStore _store = new();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Constructor_RejectsNullStore()
        => Assert.Throws<ArgumentNullException>(() => new InMemoryOutboxStorage(null));

    [Fact]
    public async Task Add_RejectsNullMessage()
    {
        var storage = new InMemoryOutboxStorage(_store);
        await Assert.ThrowsAsync<ArgumentNullException>(() => storage.AddAsync(null));
    }

    [Fact]
    public async Task SaveChanges_PersistsPendingMessagesAndDoesNotRepeatThem()
    {
        var storage = new InMemoryOutboxStorage(_store);
        await storage.AddAsync(new OutboxMessage());
        await storage.SaveChangesAsync();
        await storage.SaveChangesAsync();
        Assert.Single(_store.Messages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_RespectsAmbientTransaction(bool commit)
    {
        var storage = new InMemoryOutboxStorage(_store);
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await storage.AddAsync(new OutboxMessage());
            await storage.SaveChangesAsync();
            Assert.Empty(_store.Messages);
            if (commit)
                transaction.Complete();
        }
        Assert.Equal(commit ? 1 : 0, _store.Messages.Count);
    }

    [Fact]
    public async Task LockPending_ReturnsDueMessages()
    {
        var message = new OutboxMessage();
        _store.Add(message);
        var storage = new InMemoryOutboxStorage(_store);

        var pending = await storage.LockPendingAsync(1, TimeSpan.FromMinutes(1), _now);
        Assert.Equal(message.Id, Assert.Single(pending).Id);
    }

    [Fact]
    public async Task Lock_ReturnsLockedMessage()
    {
        var message = new OutboxMessage();
        _store.Add(message);
        var storage = new InMemoryOutboxStorage(_store);

        var locked = await storage.LockAsync(message.Id, TimeSpan.FromMinutes(1), _now);
        Assert.Equal(OutboxMessageStatus.Locked, locked.Status);
    }

    [Fact]
    public async Task MarkFailed_UpdatesStoredMessage()
    {
        var message = new OutboxMessage();
        _store.Add(message);
        var storage = new InMemoryOutboxStorage(_store);

        await storage.MarkFailedAsync(message.Id, "failure", _now, null);
        Assert.Equal(OutboxMessageStatus.Failed, Assert.Single(_store.Messages).Status);
    }

    [Fact]
    public async Task MarkPublishedAndClean_RemoveCompletedMessage()
    {
        var message = new OutboxMessage();
        _store.Add(message);
        var storage = new InMemoryOutboxStorage(_store);

        await storage.MarkPublishedAsync(message.Id, _now);
        Assert.Equal(1, await storage.CleanPublishedAsync(_now, 1));
        Assert.Empty(_store.Messages);
    }

    [Fact]
    public async Task SchemaInitializer_Completes()
        => await new InMemoryOutboxSchemaInitializer().InitializeAsync();
}
