namespace Pigeon.Messaging.Kafka.Tests
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Management;
    using Xunit;

    public class ConfigurationProviderTests
    {
        [Fact]
        public void GetConsumerConfig_Should_Disable_AutoCommit_By_Default()
        {
            var provider = CreateProvider(new GlobalSettings());

            var config = provider.GetConsumerConfig("billing");

            Assert.False(config.EnableAutoCommit);
        }

        [Fact]
        public void GetConsumerConfig_Should_Enable_AutoCommit_When_Acknowledgement_Mode_Is_OnReceive()
        {
            var provider = CreateProvider(new GlobalSettings
            {
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    AcknowledgementMode = MessageAcknowledgementMode.OnReceive
                }
            });

            var config = provider.GetConsumerConfig("billing");

            Assert.True(config.EnableAutoCommit);
        }

        private static ConfigurationProvider CreateProvider(GlobalSettings globalSettings)
            => new(
                Options.Create(globalSettings),
                Options.Create(new KafkaSettings
                {
                    BootstrapServers = "localhost:9092"
                }));
    }
}
