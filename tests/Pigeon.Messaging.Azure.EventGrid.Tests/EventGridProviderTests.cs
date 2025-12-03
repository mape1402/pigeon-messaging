using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Pigeon.Messaging.Azure.EventGrid.Tests
{
    public class EventGridProviderTests
    {
        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Options()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new EventGridProvider(null));
        }

        [Fact]
        public void GetClient_Should_Return_EventGridPublisher()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>
                {
                    ["test-topic"] = "endpoint1"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["endpoint1"] = new Endpoint
                    {
                        Url = "https://test.eventgrid.azure.net/api/events",
                        AccessKey = "test-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client = provider.GetClient("test-topic");

            // Assert
            Assert.NotNull(client);
            Assert.IsAssignableFrom<IEventGridPublisher>(client);
        }

        [Fact]
        public void GetClient_Should_Return_Same_Instance_For_Same_Topic()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>
                {
                    ["test-topic"] = "endpoint1"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["endpoint1"] = new Endpoint
                    {
                        Url = "https://test.eventgrid.azure.net/api/events",
                        AccessKey = "test-key"
                    }
                }
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client1 = provider.GetClient("test-topic");
            var client2 = provider.GetClient("test-topic");

            // Assert
            Assert.Same(client1, client2);
        }

        [Fact]
        public void GetClient_Should_Throw_For_Unconfigured_Topic()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("unknown-topic"));
            Assert.Contains("No routing key found for topic ", ex.Message);
        }

        [Fact]
        public void GetClient_Should_Throw_For_Missing_Endpoint()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>
                {
                    ["test-topic"] = "missing-endpoint"
                },
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => provider.GetClient("test-topic"));
            Assert.Contains("is not configured in the settings", ex.Message);
        }

        [Fact]
        public void CreateProcessor_Should_Return_ServiceBusProcessor()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var processor = provider.CreateProcessor("test-topic");

            // Assert
            Assert.NotNull(processor);
            Assert.IsAssignableFrom<ServiceBusProcessor>(processor);
        }

        [Fact]
        public void CreateProcessor_Should_Return_Different_Instances_For_Different_Topics()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey",
                TopicRouting = new Dictionary<string, string>(),
                Endpoints = new Dictionary<string, Endpoint>()
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var processor1 = provider.CreateProcessor("topic1");
            var processor2 = provider.CreateProcessor("topic2");

            // Assert
            Assert.NotSame(processor1, processor2);
        }
    }
}