namespace Pigeon.Messaging.RabbitMq.Tests.Producing;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Rabbit.Producing;
using RabbitMQ.Client;

public class RabbitPublisherLifecycleTests
{
    private readonly IConnectionProvider _connection = Substitute.For<IConnectionProvider>();
    private readonly ISerializer _serializer = Substitute.For<ISerializer>();

    [Fact]
    public async Task Dispose_ClosesCreatedChannels()
    {
        var channel = Substitute.For<IChannel>();
        _connection.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(channel);
        _serializer.Serialize(Arg.Any<object>()).Returns("{}");
        var adapter = CreateAdapter(new RabbitSettings { PublisherChannelPoolSize = 1 });
        await adapter.PublishRawMessageAsync("message", "orders");
        await adapter.DisposeAsync();
        await channel.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task Dispose_HandlesUnusedChannels()
    {
        var adapter = CreateAdapter(new RabbitSettings { PublisherChannelPoolSize = 2 });
        await adapter.DisposeAsync();
        await _connection.DidNotReceive().CreateChannelAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Publish_UsesConfiguredExchangeWhenRouteHasNone()
    {
        var channel = Substitute.For<IChannel>();
        _connection.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(channel);
        _serializer.Serialize(Arg.Any<object>()).Returns("{}");
        await using var adapter = CreateAdapter(new RabbitSettings { Exchange = "events", PublisherChannelPoolSize = 1 });
        await adapter.PublishRawMessageAsync("message", "orders");
        await channel.Received(1).BasicPublishAsync("events", "orders", false, Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    private RabbitProducingAdapter CreateAdapter(RabbitSettings settings)
        => new(_connection, _serializer, Options.Create(settings), NullLogger<RabbitProducingAdapter>.Instance);
}
