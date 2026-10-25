namespace Pigeon.Messaging.Kafka.Tests;

using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Management;

public class ConfigurationProviderTests
{
    [Fact]
    public void Constructor_RequiresGlobalSettingsValue()
    {
        var global = Substitute.For<IOptions<GlobalSettings>>();
        global.Value.Returns((GlobalSettings)null);
        Assert.Throws<ArgumentNullException>(() => new ConfigurationProvider(global, Options.Create(new KafkaSettings())));
    }

    [Fact]
    public void Constructor_RequiresKafkaSettingsValue()
    {
        var kafka = Substitute.For<IOptions<KafkaSettings>>();
        kafka.Value.Returns((KafkaSettings)null);
        Assert.Throws<ArgumentNullException>(() => new ConfigurationProvider(Options.Create(new GlobalSettings()), kafka));
    }

    [Fact]
    public void ConsumerConfig_NullExecutionDisablesAutoCommit()
    {
        var provider = new ConfigurationProvider(Options.Create(new GlobalSettings { Domain = "domain", ConsumerExecution = null }), Options.Create(new KafkaSettings()));
        Assert.False(provider.GetConsumerConfig().EnableAutoCommit);
    }
    [Theory]
    [InlineData(null, "domain")]
    [InlineData("", "domain")]
    [InlineData(" ", "domain")]
    [InlineData("billing", "billing")]
    public void ConsumerConfig_UsesExpectedGroup(string requested, string expected)
    {
        var provider = CreateProvider(MessageAcknowledgementMode.Manual);
        Assert.Equal(expected, provider.GetConsumerConfig(requested).GroupId);
    }

    [Theory]
    [InlineData(MessageAcknowledgementMode.Manual, false)]
    [InlineData(MessageAcknowledgementMode.OnReceive, true)]
    public void ConsumerConfig_UsesAckMode(MessageAcknowledgementMode mode, bool autoCommit)
        => Assert.Equal(autoCommit, CreateProvider(mode).GetConsumerConfig().EnableAutoCommit);

    [Fact]
    public void ProducerConfig_MapsConnectionSettings()
    {
        var result = CreateProvider(MessageAcknowledgementMode.Manual).GetProducerConfig();
        Assert.Equal("localhost:9092", result.BootstrapServers);
        Assert.Equal("user", result.SaslUsername);
        Assert.Equal("password", result.SaslPassword);
        Assert.Equal(SecurityProtocol.SaslSsl, result.SecurityProtocol);
        Assert.Equal(SaslMechanism.Plain, result.SaslMechanism);
        Assert.Equal(Acks.All, result.Acks);
    }

    private static ConfigurationProvider CreateProvider(MessageAcknowledgementMode mode)
        => new(Options.Create(new GlobalSettings { Domain = "domain", ConsumerExecution = new ConsumerExecutionSettings { AcknowledgementMode = mode } }),
            Options.Create(new KafkaSettings { BootstrapServers = "localhost:9092", UserName = "user", Password = "password" }));
}
