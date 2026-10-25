namespace Pigeon.Testing.Tests;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

public class PigeonTestingProducingAdapterTests
{
    [Fact]
    public void Constructor_RequiresTransport()
        => Assert.Throws<ArgumentNullException>(() => new PigeonTestingProducingAdapter(null));

    [Fact]
    public async Task PublishWrappedByTopic_CapturesMessage()
    {
        var transport = CreateTransport();
        await new PigeonTestingProducingAdapter(transport).PublishMessageAsync(new WrappedPayload<string> { Message = "hello" }, "orders");
        Assert.Equal("hello", Assert.Single(transport.PublishedMessages).Message);
    }

    [Fact]
    public async Task PublishRawByTopic_CapturesRawMessage()
    {
        var transport = CreateTransport();
        await new PigeonTestingProducingAdapter(transport).PublishRawMessageAsync("hello", "orders");
        Assert.True(Assert.Single(transport.PublishedMessages).IsRaw);
    }

    [Fact]
    public async Task PublishRawByRoute_PreservesRoute()
    {
        var transport = CreateTransport();
        var route = PublishingRoute.ForExchange("events", "orders");
        await new PigeonTestingProducingAdapter(transport).PublishRawMessageAsync("hello", route);
        Assert.Equal("events", Assert.Single(transport.PublishedMessages).Route.Exchange);
    }

    private static PigeonTestingTransport CreateTransport()
        => new(new ServiceCollection().BuildServiceProvider(), Substitute.For<ISerializer>(), Substitute.For<IConsumingConfigurator>(),
            Substitute.For<IConsumingDispatcher>(), Substitute.For<IRawPayloadFactory>());
}
