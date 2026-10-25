namespace Pigeon.Messaging.Tests.Producing;

using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;
using Pigeon.Messaging.Topology;

public class PublisherInvokerValidationTests
{
    [Fact]
    public void Invoker_RequiresManager()
        => Assert.Throws<ArgumentNullException>(() => new PigeonPublisherInvoker(null, Substitute.For<ISerializer>()));

    [Fact]
    public void Invoker_RequiresSerializer()
        => Assert.Throws<ArgumentNullException>(() => new PigeonPublisherInvoker(Substitute.For<IProducingManager>(), null));

    [Fact]
    public async Task Invoker_RejectsNullEnvelope()
    {
        var invoker = new PigeonPublisherInvoker(Substitute.For<IProducingManager>(), Substitute.For<ISerializer>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => invoker.PublishAsync(null).AsTask());
    }

    [Fact]
    public void Manager_RequiresAdapter()
        => Assert.Throws<ArgumentNullException>(() => new ProducingManager(null, Substitute.For<ITopologyProvisioningService>()));

    [Fact]
    public void Manager_RequiresTopologyService()
        => Assert.Throws<ArgumentNullException>(() => new ProducingManager(Substitute.For<IMessageBrokerProducingAdapter>(), null));

    [Fact]
    public async Task Manager_RejectsNullOutboxMessage()
    {
        var manager = new ProducingManager(Substitute.For<IMessageBrokerProducingAdapter>());
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.PushOutboxAsync(null).AsTask());
    }

    [Fact]
    public async Task Manager_RequiresSerializerForOutbox()
    {
        var manager = new ProducingManager(Substitute.For<IMessageBrokerProducingAdapter>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.PushOutboxAsync(new OutboxMessage()).AsTask());
    }

    [Fact]
    public async Task Manager_ReplaysRawOutboxMessageToTopic()
    {
        var adapter = Substitute.For<IMessageBrokerProducingAdapter>();
        var serializer = Substitute.For<ISerializer>();
        serializer.Deserialize("hello", typeof(string)).Returns("hello");
        var manager = new ProducingManager(adapter, Substitute.For<ITopologyProvisioningService>(), serializer);
        var message = new OutboxMessage { IsRaw = true, Payload = "hello", PayloadType = typeof(string).AssemblyQualifiedName, Topic = "orders" };
        await manager.PushOutboxAsync(message);
        await adapter.Received(1).PublishRawMessageAsync("hello", Arg.Is<PublishingRoute>(route => route.Topic == "orders"), Arg.Any<CancellationToken>());
    }
}
