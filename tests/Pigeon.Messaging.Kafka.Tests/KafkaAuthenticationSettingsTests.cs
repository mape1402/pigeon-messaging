namespace Pigeon.Messaging.Kafka.Tests;

using Confluent.Kafka;
using Microsoft.Extensions.Options;

public class KafkaAuthenticationSettingsTests
{
    [Fact]
    public void Validate_AcceptsBootstrapServers()
        => KafkaSettingsValidator.Validate(new KafkaSettings { BootstrapServers = "kafka.example:9092" });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_RejectsMissingBootstrapServers(string bootstrapServers)
        => Assert.Throws<InvalidOperationException>(() => KafkaSettingsValidator.Validate(new KafkaSettings
        {
            BootstrapServers = bootstrapServers
        }));

    [Fact]
    public void ConfigurationProvider_AppliesProgrammaticClientConfiguration()
    {
        var provider = new ConfigurationProvider(
            Options.Create(new GlobalSettings { Domain = "orders" }),
            Options.Create(new KafkaSettings
            {
                BootstrapServers = "kafka.example:9092",
                ConfigureProducer = config => config.ClientId = "pigeon-producer",
                ConfigureConsumer = config => config.ClientId = "pigeon-consumer"
            }));

        Assert.Equal("pigeon-producer", provider.GetProducerConfig().ClientId);
        Assert.Equal("pigeon-consumer", provider.GetConsumerConfig().ClientId);
    }

    [Fact]
    public void ConfigurationProvider_ExposesOAuthRefreshHandler()
    {
        Action<IClient, string> handler = (_, _) => { };
        var provider = new ConfigurationProvider(
            Options.Create(new GlobalSettings()),
            Options.Create(new KafkaSettings
            {
                BootstrapServers = "kafka.example:9092",
                OAuthBearerTokenRefreshHandler = handler
            }));

        Assert.Same(handler, provider.GetOAuthBearerTokenRefreshHandler());
    }
}
