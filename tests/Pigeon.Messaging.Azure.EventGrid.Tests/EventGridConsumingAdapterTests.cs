using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;

namespace Pigeon.Messaging.Azure.EventGrid.Tests.Consuming
{
    public class EventGridConsumingAdapterTests
    {
        [Fact]
        public async Task StartConsumeAsync_Should_Create_Processors_For_All_Topics()
        {
            // Arrange
            var topics = new[] { "topic1", "topic2" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor1 = Substitute.For<ServiceBusProcessor>();
            var processor2 = Substitute.For<ServiceBusProcessor>();
            
            provider.CreateProcessor("topic1").Returns(processor1);
            provider.CreateProcessor("topic2").Returns(processor2);
            
            processor1.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            processor2.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
            await processor1.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
            await processor2.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
            
            // Verificar el logging
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("AzureEventGridConsumingAdapter has been initialized")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task StartConsumeAsync_Should_Create_Processors_And_StartProcessing()
        {
            // Arrange
            var domain = "domain";
            var topic = "topic1";

            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllTopics().Returns([topic]);

            var processor = new ServiceBusClient("Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc")
                .CreateProcessor(topic, new ServiceBusProcessorOptions());

            var provider = Substitute.For<IEventGridProvider>();
            provider.CreateProcessor(topic).Returns(processor);

            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = domain });
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

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

            var provider = Substitute.For<IEventGridProvider>();
            provider.CreateProcessor(topic).Returns(processor);

            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = domain });
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();
            await adapter.StopConsumeAsync();

            // Assert
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Dependencies()
        {
            // Arrange
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(null, provider, options, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(configurator, null, options, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(configurator, provider, null, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(configurator, provider, options, null));
        }

        [Fact]
        public async Task MessageConsumed_Event_Should_Be_Available()
        {
            // Arrange
            var topics = new[] { "topic1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor = Substitute.For<ServiceBusProcessor>();
            provider.CreateProcessor("topic1").Returns(processor);
            processor.StartProcessingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);
            MessageConsumedEventArgs capturedEventArgs = null;
            adapter.MessageConsumed += (sender, args) => capturedEventArgs = args;

            // Act
            await adapter.StartConsumeAsync();

            // Assert that the adapter is properly initialized
            provider.Received(1).CreateProcessor("topic1");
            await processor.Received(1).StartProcessingAsync(Arg.Any<CancellationToken>());
        }
    }
}