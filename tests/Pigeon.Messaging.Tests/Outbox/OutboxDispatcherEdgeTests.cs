namespace Pigeon.Messaging.Tests.Outbox;

using NSubstitute;
using Pigeon.Messaging.Outbox;

public class OutboxDispatcherEdgeTests
{
    [Fact]
    public async Task Initialize_AutoCreatesSchema()
    {
        using var fixture = new OutboxDispatcherFixture();
        fixture.Settings.SchemaMode = OutboxSchemaMode.AutoCreate;
        await fixture.InvokeAsync("InitializeSchemaAsync", CancellationToken.None);
        await fixture.Initializer.Received(1).InitializeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Initialize_ManualModeDoesNotCreateSchema()
    {
        using var fixture = new OutboxDispatcherFixture();
        fixture.Settings.SchemaMode = OutboxSchemaMode.Manual;
        await fixture.InvokeAsync("InitializeSchemaAsync", CancellationToken.None);
        await fixture.Initializer.DidNotReceive().InitializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Recover_QueuesEveryLockedMessage()
    {
        using var fixture = new OutboxDispatcherFixture();
        var message = new OutboxMessage { Id = Guid.NewGuid() };
        fixture.Storage.LockPendingAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new[] { message });
        await fixture.InvokeAsync("QueuePendingMessagesAsync", CancellationToken.None);
        await fixture.Queue.Received(1).EnqueueAsync(message.Id, CancellationToken.None);
    }

    [Fact]
    public async Task Recover_RequiresStorage()
    {
        using var fixture = new OutboxDispatcherFixture(includeStorage: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InvokeAsync("QueuePendingMessagesAsync", CancellationToken.None));
    }

    [Fact]
    public async Task Recover_ClampsBatchSizeAndUsesDefaultLockTimeout()
    {
        using var fixture = new OutboxDispatcherFixture();
        fixture.Settings.DispatchBatchSize = 0;
        fixture.Settings.LockTimeout = TimeSpan.Zero;
        await fixture.InvokeAsync("QueuePendingMessagesAsync", CancellationToken.None);
        await fixture.Storage.Received(1).LockPendingAsync(1, TimeSpan.FromMinutes(5), Arg.Any<DateTimeOffset>(), CancellationToken.None);
    }

    [Fact]
    public async Task Dispatch_PublishesAndMarksSuccessfulMessage()
    {
        using var fixture = new OutboxDispatcherFixture();
        var message = new OutboxMessage { Id = Guid.NewGuid() };
        fixture.Storage.LockAsync(message.Id, Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(message);
        await fixture.InvokeAsync("DispatchMessageAsync", message.Id, CancellationToken.None);
        await fixture.Producer.Received(1).PushOutboxAsync(message, CancellationToken.None);
        await fixture.Storage.Received(1).MarkPublishedAsync(message.Id, Arg.Any<DateTimeOffset>(), CancellationToken.None);
        await fixture.Storage.Received(2).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Dispatch_IgnoresUnavailableMessage()
    {
        using var fixture = new OutboxDispatcherFixture();
        await fixture.InvokeAsync("DispatchMessageAsync", Guid.NewGuid(), CancellationToken.None);
        await fixture.Producer.DidNotReceive().PushOutboxAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_RequiresStorage()
    {
        using var fixture = new OutboxDispatcherFixture(includeStorage: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InvokeAsync("DispatchMessageAsync", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Dispatch_MarksExhaustedMessageWithoutRetry()
    {
        using var fixture = new OutboxDispatcherFixture();
        fixture.Settings.MaxRetries = 1;
        var message = new OutboxMessage { Id = Guid.NewGuid() };
        fixture.Storage.LockAsync(message.Id, Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(message);
        fixture.Producer.PushOutboxAsync(message, Arg.Any<CancellationToken>()).Returns(ValueTask.FromException(new InvalidOperationException("failed")));
        await fixture.InvokeAsync("DispatchMessageAsync", message.Id, CancellationToken.None);
        await fixture.Storage.Received(1).MarkFailedAsync(message.Id, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), null, CancellationToken.None);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Cleanup_SavesOnlyWhenRowsWereRemoved(int removed)
    {
        using var fixture = new OutboxDispatcherFixture();
        fixture.Storage.CleanPublishedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(removed);
        await fixture.InvokeAsync("CleanPublishedAsync", CancellationToken.None);
        await fixture.InvokeAsync("CleanPublishedAsync", CancellationToken.None);
        await fixture.Storage.Received(1).CleanPublishedAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await fixture.Storage.Received(removed).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Cleanup_WithoutStorageCompletes()
    {
        using var fixture = new OutboxDispatcherFixture(includeStorage: false);
        await fixture.InvokeAsync("CleanPublishedAsync", CancellationToken.None);
    }

    [Fact]
    public async Task QueueWorker_ContinuesAfterFailureAndStopsOnCancellation()
    {
        using var fixture = new OutboxDispatcherFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Queue.DequeueAsync(Arg.Any<CancellationToken>()).Returns(
            _ => ValueTask.FromException<Guid>(new InvalidOperationException()),
            _ => { cancellation.Cancel(); return ValueTask.FromCanceled<Guid>(cancellation.Token); });
        await fixture.InvokeAsync("ProcessQueuedMessagesAsync", cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Queue.Received(2).DequeueAsync(cancellation.Token);
    }

    [Fact]
    public async Task QueueWorker_DispatchesDequeuedMessage()
    {
        using var fixture = new OutboxDispatcherFixture();
        using var cancellation = new CancellationTokenSource();
        var id = Guid.NewGuid();
        fixture.Queue.DequeueAsync(Arg.Any<CancellationToken>()).Returns(
            _ => ValueTask.FromResult(id),
            _ => { cancellation.Cancel(); return ValueTask.FromCanceled<Guid>(cancellation.Token); });
        await fixture.InvokeAsync("ProcessQueuedMessagesAsync", cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Storage.Received(1).LockAsync(id, Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), cancellation.Token);
    }

    [Fact]
    public async Task RecoveryWorker_StopsWhenStorageCancels()
    {
        using var fixture = new OutboxDispatcherFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Storage.LockPendingAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => { cancellation.Cancel(); return Task.FromCanceled<IReadOnlyCollection<OutboxMessage>>(cancellation.Token); });
        await fixture.InvokeAsync("RecoverPendingMessagesAsync", cancellation.Token).WaitAsync(TimeSpan.FromSeconds(5));
        await fixture.Storage.Received(1).LockPendingAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), cancellation.Token);
    }

    [Fact]
    public async Task RecoveryWorker_HandlesStorageFailureBeforeCancellation()
    {
        using var fixture = new OutboxDispatcherFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Storage.LockPendingAsync(Arg.Any<int>(), Arg.Any<TimeSpan>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(_ => { cancellation.Cancel(); return Task.FromException<IReadOnlyCollection<OutboxMessage>>(new InvalidOperationException()); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.InvokeAsync("RecoverPendingMessagesAsync", cancellation.Token));
    }
}
