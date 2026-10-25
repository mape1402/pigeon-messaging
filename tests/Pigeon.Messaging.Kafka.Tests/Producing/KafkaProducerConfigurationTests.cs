namespace Pigeon.Messaging.Kafka.Tests.Producing;

using Confluent.Kafka;
using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Kafka.Producing;
using System.Reflection;

public class KafkaProducerConfigurationTests
{
    [Fact]
    public void Constructor_CreatesWrappedAndRawProducers()
    {
        var configuration = Substitute.For<IConfigurationProvider>();
        configuration.GetProducerConfig().Returns(new ProducerConfig { BootstrapServers = "127.0.0.1:1" });
        var producer = new KafkaProducer<string>(configuration, Substitute.For<ISerializer>());
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var wrapped = (IProducer<Null, WrappedPayload<string>>)typeof(KafkaProducer<string>).GetField("_producer", flags)!.GetValue(producer)!;
        var raw = (IProducer<Null, string>)typeof(KafkaProducer<string>).GetField("_rawProducer", flags)!.GetValue(producer)!;
        try
        {
            Assert.NotNull(wrapped);
            Assert.NotNull(raw);
            configuration.Received(1).GetProducerConfig();
        }
        finally
        {
            wrapped.Dispose();
            raw.Dispose();
        }
    }
}
