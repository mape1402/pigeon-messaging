namespace Pigeon.Messaging.Kafka.Tests;

using Confluent.Kafka;

public class KafkaClientBuilderAuthenticationTests
{
    [Fact]
    public void Apply_ProducerBuilder_ReturnsConfiguredBuilder()
    {
        var builder = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = "kafka.example:9092" });

        var result = KafkaClientBuilderAuthentication.Apply(builder, static (_, _) => { });

        Assert.Same(builder, result);
    }

    [Fact]
    public void Apply_ConsumerBuilder_ReturnsConfiguredBuilder()
    {
        var builder = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = "kafka.example:9092",
            GroupId = "pigeon-tests"
        });

        var result = KafkaClientBuilderAuthentication.Apply(builder, static (_, _) => { });

        Assert.Same(builder, result);
    }

    [Fact]
    public void Apply_AdminClientBuilder_ReturnsConfiguredBuilder()
    {
        var builder = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = "kafka.example:9092" });

        var result = KafkaClientBuilderAuthentication.Apply(builder, static (_, _) => { });

        Assert.Same(builder, result);
    }

    [Fact]
    public void Apply_AdminClientBuilder_ReturnsOriginalBuilderWithoutHandler()
    {
        var builder = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = "kafka.example:9092" });

        var result = KafkaClientBuilderAuthentication.Apply(builder, null);

        Assert.Same(builder, result);
    }
}
