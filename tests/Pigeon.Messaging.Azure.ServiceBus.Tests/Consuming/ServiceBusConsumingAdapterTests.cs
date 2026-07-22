using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Consuming;
using Pigeon.Messaging.Consuming.Configuration;

namespace Pigeon.Messaging.Azure.ServiceBus.Tests.Consuming
{
    public class ServiceBusConsumingAdapterTests
    {
        [Fact]
        public async Task StartConsumeAsync_Should_Create_Processors_And_StartProcessing()
        {
            // Arrange
            var domain = "domain";
            var topic = "topic1";

            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllTopics().Returns([ topic ]);

            var processor = new ServiceBusClient("Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc")
                .CreateProcessor(topic, new ServiceBusProcessorOptions());

            var provider = Substitute.For<IServiceBusProvider>();
            provider.CreateProcessor(topic).Returns(processor);

            var logger = Substitute.For<ILogger<ServiceBusConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = domain });
            var adapter = new ServiceBusConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
        }

        [Fact]
        public async Task StopConsumeAsync_Should_Stop_And_Dispose_Processors()
        {
            // Arrange
            var domain = "domain";
            var topic = "topic1";

            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllTopics().Returns([topic]);

            var processor = new ServiceBusClient("Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc")
                .CreateProcessor(topic, new ServiceBusProcessorOptions());

            var provider = Substitute.For<IServiceBusProvider>();
            provider.CreateProcessor(topic).Returns(processor);

            var logger = Substitute.For<ILogger<ServiceBusConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = domain });
            var adapter = new ServiceBusConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();
            await adapter.StopConsumeAsync();

            // Assert
        }

        [Fact]
        public async Task StartConsumeAsync_Should_Create_Subscription_Processor_For_Endpoint()
        {
            var topic = "user.created";
            var subscription = "billing";
            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllEndpoints().Returns([new ConsumerEndpoint(topic, subscription)]);

            var processor = Substitute.For<ServiceBusProcessor>();
            processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

            var provider = Substitute.For<IServiceBusProvider>();
            provider.CreateProcessor(topic, subscription).Returns(processor);

            var logger = Substitute.For<ILogger<ServiceBusConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "domain" });
            var adapter = new ServiceBusConsumingAdapter(configurator, provider, options, logger);

            await adapter.StartConsumeAsync();

            provider.Received(1).CreateProcessor(topic, subscription);
            provider.DidNotReceive().CreateProcessor(topic);
            await processor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
        }
    }
}
