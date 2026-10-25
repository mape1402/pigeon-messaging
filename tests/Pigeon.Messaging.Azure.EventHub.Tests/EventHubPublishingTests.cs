namespace Pigeon.Messaging.Azure.EventHub.Tests;

using global::Azure.Messaging.EventHubs;
using global::Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Producing;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

public class EventHubPublishingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_SendsBatch(bool raw)
    {
        var (adapter, producer, events) = CreateAdapter(true);
        if (raw) await adapter.PublishRawMessageAsync("hello", "orders");
        else await adapter.PublishMessageAsync(CreatePayload(), "orders");
        await producer.Received(1).SendAsync(Arg.Any<EventDataBatch>(), Arg.Any<CancellationToken>());
        Assert.Single(events);
        Assert.Equal("orders", events[0].Properties["RoutingKey"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_RejectsOversizedEvent(bool raw)
    {
        var (adapter, producer, _) = CreateAdapter(false);
        if (raw) await Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.PublishRawMessageAsync("hello", "orders"));
        else await Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.PublishMessageAsync(CreatePayload(), "orders"));
        await producer.DidNotReceive().SendAsync(Arg.Any<EventDataBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishRaw_PropagatesBatchCreationFailure()
    {
        var (adapter, producer, _) = CreateAdapter(true);
        producer.CreateBatchAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromException<EventDataBatch>(new InvalidOperationException("unavailable")));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.PublishRawMessageAsync("hello", "orders"));
    }

    [Fact]
    public void WrappedEvent_IncludesMetadataAndRoute()
    {
        var (adapter, _, _) = CreateAdapter(true);
        var result = adapter.CreateEventData(CreatePayload(), PublishingRoute.ForExchange("events", "orders"));
        Assert.Equal("sales", result.Properties["Domain"]);
        Assert.Equal("1.0.0", result.Properties["MessageVersion"]);
        Assert.Equal("events", result.Properties["Exchange"]);
    }

    private static WrappedPayload<string> CreatePayload() => new()
    {
        Message = "hello", Domain = "sales", MessageVersion = new SemanticVersion(1, 0, 0), CreatedOnUtc = DateTimeOffset.UtcNow
    };

    private static (EventHubProducingAdapter, EventHubProducerClient, List<EventData>) CreateAdapter(bool accepts)
    {
        var events = new List<EventData>();
        var batch = EventHubsModelFactory.EventDataBatch(10000, events, new CreateBatchOptions(), _ => accepts);
        var producer = Substitute.For<EventHubProducerClient>();
        producer.CreateBatchAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(batch));
        var provider = Substitute.For<IEventHubProvider>();
        provider.GetProducer("orders").Returns(producer);
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        return (new EventHubProducingAdapter(provider, serializer, NullLogger<EventHubProducingAdapter>.Instance), producer, events);
    }
}
