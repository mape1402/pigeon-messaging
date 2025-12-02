using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub;
using global::Azure.Messaging.EventHubs.Producer;

namespace Pigeon.Messaging.Azure.EventHub.Tests
{
    public class EventHubProviderTests
    {
        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Options()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventHubProvider(null));
        }

        [Fact]
        public void GetProducer_Should_Return_EventHubProducerClient()
        {
            // Arrange
            var settings = new AzureEventHubSettings
            {
                ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key"
            };
            var options = Options.Create(settings);
            var provider = new EventHubProvider(options);

            // Act
            var producer = provider.GetProducer("test-hub");

            // Assert
            Assert.NotNull(producer);
            Assert.IsType<EventHubProducerClient>(producer);
        }

        [Fact]
        public void GetProducer_Should_Return_Same_Instance_For_Same_Hub()
        {
            // Arrange
            var settings = new AzureEventHubSettings
            {
                ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key"
            };
            var options = Options.Create(settings);
            var provider = new EventHubProvider(options);

            // Act
            var producer1 = provider.GetProducer("test-hub");
            var producer2 = provider.GetProducer("test-hub");

            // Assert
            Assert.Same(producer1, producer2);
        }

        [Fact]
        public void GetProducer_Should_Return_Different_Instances_For_Different_Hubs()
        {
            // Arrange
            var settings = new AzureEventHubSettings
            {
                ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key"
            };
            var options = Options.Create(settings);
            var provider = new EventHubProvider(options);

            // Act
            var producer1 = provider.GetProducer("hub1");
            var producer2 = provider.GetProducer("hub2");

            // Assert
            Assert.NotSame(producer1, producer2);
        }

        [Fact]
        public void CreateProcessor_Should_Return_EventHubProcessor()
        {
            // Arrange
            var settings = new AzureEventHubSettings
            {
                ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key",
                ConsumerGroup = "$Default"
            };
            var options = Options.Create(settings);
            var provider = new EventHubProvider(options);

            // Act
            var processor = provider.CreateProcessor("test-hub");

            // Assert
            Assert.NotNull(processor);
            Assert.IsAssignableFrom<IEventHubProcessor>(processor);
        }

        [Fact]
        public void CreateProcessor_Should_Return_Different_Instances_For_Different_Hubs()
        {
            // Arrange
            var settings = new AzureEventHubSettings
            {
                ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key",
                ConsumerGroup = "$Default"
            };
            var options = Options.Create(settings);
            var provider = new EventHubProvider(options);

            // Act
            var processor1 = provider.CreateProcessor("hub1");
            var processor2 = provider.CreateProcessor("hub2");

            // Assert
            Assert.NotSame(processor1, processor2);
        }
    }
}