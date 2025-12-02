using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using global::Azure.Messaging.EventHubs;

namespace Pigeon.Messaging.Azure.EventHub.Tests.Consuming
{
    public class EventHubConsumingAdapterTests
    {
        [Fact]
        public void StartConsumeAsync_Should_Create_Processors_For_All_Topics()
        {
            // Arrange
            var topics = new[] { "hub1", "hub2" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor1 = Substitute.For<IEventHubProcessor>();
            var processor2 = Substitute.For<IEventHubProcessor>();
            
            provider.CreateProcessor("hub1").Returns(processor1);
            provider.CreateProcessor("hub2").Returns(processor2);
            
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);

            // Act
            adapter.StartConsumeAsync();

            // Assert
            provider.Received(1).CreateProcessor("hub1");
            provider.Received(1).CreateProcessor("hub2");
            
            // Verificar que se logueó la información
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(), 
                Arg.Is<object>(v => v.ToString().Contains("AzureEventHubConsumingAdapter has been initialized")), 
                Arg.Any<Exception>(), 
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task StopConsumeAsync_Should_Dispose_All_Processors()
        {
            // Arrange
            var topics = new[] { "hub1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor = Substitute.For<IEventHubProcessor>();
            provider.CreateProcessor("hub1").Returns(processor);
            
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();
            await adapter.StopConsumeAsync();

            // Assert
            processor.Received(1).Dispose();
            
            // Verificar que se logueó la información de parada
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(), 
                Arg.Is<object>(v => v.ToString().Contains("AzureEventHubConsumingAdapter has been stopped gracefully")), 
                Arg.Any<Exception>(), 
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public void StartConsumeAsync_Should_Handle_Duplicate_Processor_Creation()
        {
            // Arrange
            var topics = new[] { "hub1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor = Substitute.For<IEventHubProcessor>();
            provider.CreateProcessor("hub1").Returns(processor);
            
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);

            // Act
            adapter.StartConsumeAsync();
            adapter.StartConsumeAsync(); // Second call should handle gracefully

            // Assert
            provider.Received().CreateProcessor("hub1"); // Called at least once
        }

        [Fact]
        public void StartConsumeAsync_Should_Log_Error_For_Processor_Creation_Exception()
        {
            // Arrange
            var topics = new[] { "hub1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            provider.CreateProcessor("hub1").Returns<IEventHubProcessor>(_ => throw new Exception("Creation failed"));
            
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);

            // Act
            adapter.StartConsumeAsync();

            // Assert - Verificar que se logueó el error
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(), 
                Arg.Is<object>(v => v.ToString().Contains("Error starting processor for topic")), 
                Arg.Any<Exception>(), 
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Dependencies()
        {
            // Arrange
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubConsumingAdapter(null, provider, options, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubConsumingAdapter(configurator, null, options, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubConsumingAdapter(configurator, provider, null, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubConsumingAdapter(configurator, provider, options, null));
        }

        [Fact]
        public void MessageConsumed_Event_Should_Be_Available()
        {
            // Arrange
            var topics = new[] { "hub1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor = Substitute.For<IEventHubProcessor>();
            provider.CreateProcessor("hub1").Returns(processor);
            
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);
            MessageConsumedEventArgs capturedEventArgs = null;
            adapter.MessageConsumed += (sender, args) => capturedEventArgs = args;

            // Act
            adapter.StartConsumeAsync();

            // Assert
            provider.Received(1).CreateProcessor("hub1");
            // Note: The actual event processing logic would be tested through integration tests
            // as it involves complex async enumerable handling that is difficult to mock properly
        }

        [Fact]
        public async Task StopConsumeAsync_Should_Handle_Disposal_Errors_Gracefully()
        {
            // Arrange
            var topics = new[] { "hub1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var processor = Substitute.For<IEventHubProcessor>();
            processor.When(p => p.Dispose()).Do(_ => throw new Exception("Disposal failed"));
            provider.CreateProcessor("hub1").Returns(processor);
            
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();
            await adapter.StopConsumeAsync();

            // Assert - Verificar que se logueó el error
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(), 
                Arg.Is<object>(v => v.ToString().Contains("Error while stopping processor for topic")), 
                Arg.Any<Exception>(), 
                Arg.Any<Func<object, Exception, string>>());
        }
    }
}