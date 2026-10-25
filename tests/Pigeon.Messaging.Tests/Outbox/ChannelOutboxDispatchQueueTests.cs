namespace Pigeon.Messaging.Tests.Outbox;

using Microsoft.Extensions.Options;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;

public class ChannelOutboxDispatchQueueTests
{
    [Fact]
    public async Task NullSettings_UseUnboundedQueue()
    {
        var queue = new ChannelOutboxDispatchQueue(null);
        var id = Guid.NewGuid();
        await queue.EnqueueAsync(id);
        Assert.Equal(id, await queue.DequeueAsync());
    }

    [Fact]
    public async Task BoundedQueue_WaitsUntilCapacityIsAvailable()
    {
        var queue = new ChannelOutboxDispatchQueue(Options.Create(new GlobalSettings { Outbox = new OutboxSettings { DispatchQueueCapacity = 1 } }));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await queue.EnqueueAsync(first);
        var pending = queue.EnqueueAsync(second).AsTask();
        Assert.False(pending.IsCompleted);
        Assert.Equal(first, await queue.DequeueAsync());
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(second, await queue.DequeueAsync());
    }
}
