namespace Pigeon.Messaging.Tests.Consuming.Configuration;

using Pigeon.Messaging.Consuming.Configuration;

public class TopicEventArgsTests
{
    [Fact]
    public void Constructor_RequiresTopic()
        => Assert.Throws<ArgumentNullException>(() => new TopicEventArgs(null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_UsesDefaultSubscription(string subscription)
    {
        var args = new TopicEventArgs("orders", subscription);
        Assert.Equal(ConsumerEndpoint.DefaultSubscription, args.Subscription);
    }

    [Fact]
    public void Endpoint_UsesConfiguredTopicAndSubscription()
    {
        var args = new TopicEventArgs("orders", "billing");
        Assert.Equal("orders", args.Topic);
        Assert.Equal("billing", args.Subscription);
        Assert.Equal("orders::billing", args.Endpoint.Key);
    }

    [Fact]
    public void SingleArgument_UsesDefaultSubscription()
        => Assert.Equal(ConsumerEndpoint.DefaultSubscription, new TopicEventArgs("orders").Subscription);
}
