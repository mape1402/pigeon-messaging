using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Kafka.Consuming;
using System.Reflection;

namespace Pigeon.Messaging.Kafka.Tests.Consuming
{
    public class KafkaConsumingAdapterTests
    {
        [Fact]
        public async Task StopConsumeAsync_Should_Cancel_Listeners_And_Dispose_Consumers()
        {
            // Arrange
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            consumingConfigurator.GetAllTopics().Returns(new List<string> { "topic1" });
            var configProvider = Substitute.For<IConfigurationProvider>();

            var config = new ConsumerConfig
            {
                BootstrapServers = "localhost:9092",
                GroupId = "domain"
            };

            configProvider.GetConsumerConfig().Returns(config);
            var globalSettings = Options.Create(new GlobalSettings { Domain = "domain" });
            var logger = Substitute.For<ILogger<KafkaConsumingAdapter>>();
            var adapter = new KafkaConsumingAdapter(consumingConfigurator, configProvider, globalSettings, logger);

            // Start consuming (will create listeners and consumers)
            await adapter.StartConsumeAsync();

            // Act
            await adapter.StopConsumeAsync();

            // Assert
        }

        [Fact]
        public async Task Listen_Should_Invoke_MessageConsumed_On_Consume()
        {
            // Arrange
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var configProvider = Substitute.For<IConfigurationProvider>();
            var globalSettings = Options.Create(new GlobalSettings { Domain = "domain" });
            var logger = Substitute.For<ILogger<KafkaConsumingAdapter>>();
            var adapter = new KafkaConsumingAdapter(consumingConfigurator, configProvider, globalSettings, logger);
            var consumer = Substitute.For<IConsumer<Ignore, string>>();
            var tokenSource = new CancellationTokenSource();
            var called = false;
            adapter.MessageConsumed += (s, e) => called = true;
            consumer.Consume(Arg.Any<CancellationToken>()).Returns(new ConsumeResult<Ignore, string> { Topic = "topic", Message = new Message<Ignore, string> { Value = "msg" } });

            // Act
            var listenTask = Task.Run(() => adapter.GetType().GetMethod("Listen", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(adapter, [ consumer, tokenSource.Token ]));
            tokenSource.CancelAfter(100);
            await Task.Delay(200);

            // Assert
            Assert.True(called);
        }
    }
}
