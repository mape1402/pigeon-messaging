using Confluent.Kafka;
using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Kafka.Producing;

namespace Pigeon.Messaging.Kafka.Tests.Producing;

public class KafkaProducerTests
{
    private readonly IProducer<Null, WrappedPayload<string>> _wrapped = Substitute.For<IProducer<Null, WrappedPayload<string>>>();
    private readonly IProducer<Null, string> _raw = Substitute.For<IProducer<Null, string>>();

    [Fact]
    public void Constructor_RejectsNullProducers()
    {
        Assert.Throws<ArgumentNullException>(() => new KafkaProducer<string>(null, _raw));
        Assert.Throws<ArgumentNullException>(() => new KafkaProducer<string>(_wrapped, null));
    }

    [Fact]
    public async Task Publish_SendsWrappedPayloadToTopic()
    {
        var payload = new WrappedPayload<string> { Message = "hello" };
        var producer = new KafkaProducer<string>(_wrapped, _raw);
        await producer.PublishAsync(payload, "orders");
        await _wrapped.Received(1).ProduceAsync("orders", Arg.Is<Message<Null, WrappedPayload<string>>>(message => message.Value == payload), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishRaw_SendsUnwrappedPayloadToTopic()
    {
        var producer = new KafkaProducer<string>(_wrapped, _raw);
        await producer.PublishRawAsync("hello", "orders");
        await _raw.Received(1).ProduceAsync("orders", Arg.Is<Message<Null, string>>(message => message.Value == "hello"), Arg.Any<CancellationToken>());
    }
}
