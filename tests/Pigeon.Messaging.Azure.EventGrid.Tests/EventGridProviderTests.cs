using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid;
using global::Azure.Messaging.EventGrid;
using global::Azure.Messaging.ServiceBus;

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
                TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
                AccessKey = "test-key",
                ServiceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey"
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var client = provider.GetClient("test-topic");

            // Assert
            Assert.NotNull(client);
            Assert.IsAssignableFrom<IEventGridPublisher>(client);
        }
    }
}