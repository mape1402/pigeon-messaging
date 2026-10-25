using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Producing;

namespace Pigeon.Testing.Tests;

public class PigeonTestingAdapterTests
{
    [Fact]
    public async Task TopologyAdapter_CompletesWithoutExternalResources()
    {
        var adapter = new PigeonTestingTopologyAdapter();
        Assert.Equal("PigeonTesting", adapter.BrokerName);
        await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));
        await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
    }

    [Fact]
    public async Task ConsumingAdapter_StartsStopsAndAcceptsEventSubscriptions()
    {
        IMessageBrokerConsumingAdapter adapter = new PigeonTestingConsumingAdapter();
        EventHandler<MessageConsumedEventArgs> sync = (_, _) => { };
        MessageConsumedAsyncHandler async = (_, _, _) => ValueTask.CompletedTask;
        adapter.MessageConsumed += sync;
        adapter.MessageConsumedAsync += async;
        await adapter.StartConsumeAsync();
        await adapter.StopConsumeAsync();
        adapter.MessageConsumed -= sync;
        adapter.MessageConsumedAsync -= async;
    }
}
