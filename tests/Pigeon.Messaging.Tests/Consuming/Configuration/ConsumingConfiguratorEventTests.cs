namespace Pigeon.Messaging.Tests.Consuming.Configuration;

using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Contracts;

public class ConsumingConfiguratorEventTests
{
    [Fact]
    public void AddConsumer_NotifiesSubscriber()
    {
        var configurator = new ConsumingConfigurator();
        TopicEventArgs received = null;
        configurator.TopicCreated += (_, args) => received = args;
        configurator.AddConsumer<string>("orders", (_, _) => Task.CompletedTask);
        Assert.Equal("orders", received.Topic);
    }

    [Fact]
    public void RemoveConsumer_NotifiesSubscriber()
    {
        var configurator = new ConsumingConfigurator();
        TopicEventArgs received = null;
        configurator.TopicRemoved += (_, args) => received = args;
        configurator.AddConsumer<string>("orders", (_, _) => Task.CompletedTask);
        configurator.RemoveConsumer("orders");
        Assert.Equal("orders", received.Topic);
    }

    [Fact]
    public void AddConsumer_NormalizesBlankSubscription()
    {
        var configurator = new ConsumingConfigurator();
        configurator.AddConsumer<string>("orders", SemanticVersion.Default, " ", (_, _) => Task.CompletedTask);
        Assert.Equal(ConsumerEndpoint.DefaultSubscription, configurator.GetConfiguration("orders").Subscription);
    }
}
