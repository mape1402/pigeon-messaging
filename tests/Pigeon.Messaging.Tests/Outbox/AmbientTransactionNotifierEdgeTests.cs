namespace Pigeon.Messaging.Tests.Outbox;

using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;
using System.Reflection;

public class AmbientTransactionNotifierEdgeTests
{
    [Fact]
    public void Constructor_RequiresQueue()
        => Assert.Throws<ArgumentNullException>(() => new AmbientTransactionOutboxCommitNotifier(null, Options.Create(new GlobalSettings())));

    [Fact]
    public void Constructor_RequiresSettings()
        => Assert.Throws<ArgumentNullException>(() => new AmbientTransactionOutboxCommitNotifier(Substitute.For<IOutboxDispatchQueue>(), null));

    [Fact]
    public async Task CommittedEnqueueFailure_IsHandledInBackground()
    {
        var queue = Substitute.For<IOutboxDispatchQueue>();
        queue.EnqueueAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromException(new InvalidOperationException()));
        var notifier = new AmbientTransactionOutboxCommitNotifier(queue, Options.Create(new GlobalSettings()));
        var method = typeof(AmbientTransactionOutboxCommitNotifier).GetMethod("EnqueueCommittedMessageAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var id = Guid.NewGuid();
        await (Task)method.Invoke(notifier, new object[] { id })!;
        await queue.Received(1).EnqueueAsync(id, Arg.Any<CancellationToken>());
    }
}
