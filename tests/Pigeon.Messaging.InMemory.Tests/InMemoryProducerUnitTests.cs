namespace Pigeon.Messaging.InMemory.Tests;

using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

public class InMemoryProducerUnitTests
{
    [Fact]
    public void Constructor_RequiresBroker()
        => Assert.Throws<ArgumentNullException>(() => new InMemoryProducingAdapter(null, Substitute.For<ISerializer>()));

    [Fact]
    public void Constructor_RequiresSerializer()
        => Assert.Throws<ArgumentNullException>(() => new InMemoryProducingAdapter(new InMemoryMessageBroker(), null));

    [Fact]
    public async Task PublishWrappedByRoute_PreservesRoute()
    {
        var broker = new InMemoryMessageBroker();
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        var route = PublishingRoute.ForExchange("events", "orders");
        await new InMemoryProducingAdapter(broker, serializer).PublishMessageAsync(new WrappedPayload<string> { Message = "hello" }, route);
        Assert.Same(route, Assert.Single(broker.PublishedMessages).Route);
    }

    [Fact]
    public async Task PublishRawByRoute_SetsRawFlag()
    {
        var broker = new InMemoryMessageBroker();
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        await new InMemoryProducingAdapter(broker, serializer).PublishRawMessageAsync("hello", PublishingRoute.ForTopic("orders"));
        Assert.True(Assert.Single(broker.PublishedMessages).IsRaw);
    }

    [Fact]
    public async Task PublishWrappedByTopic_UsesTopicRoute()
    {
        var broker = new InMemoryMessageBroker();
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        await new InMemoryProducingAdapter(broker, serializer).PublishMessageAsync(new WrappedPayload<string> { Message = "hello" }, "orders");
        Assert.Equal("orders", Assert.Single(broker.PublishedMessages).Route.Topic);
    }

    [Fact]
    public async Task PublishRawByTopic_UsesTopicRoute()
    {
        var broker = new InMemoryMessageBroker();
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        await new InMemoryProducingAdapter(broker, serializer).PublishRawMessageAsync("hello", "orders");
        Assert.Equal("orders", Assert.Single(broker.PublishedMessages).Route.Topic);
    }
}
