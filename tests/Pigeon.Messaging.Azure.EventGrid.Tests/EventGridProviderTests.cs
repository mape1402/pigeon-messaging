using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid;
using global::Azure.Messaging.EventGrid;

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
                AccessKey = "test-key"
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
                TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
                AccessKey = "test-key"
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
        public void CreateSubscription_Should_Return_EventGridSubscription()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
                AccessKey = "test-key",
                WebhookEndpoint = "https://test-webhook.com/events"
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var subscription = provider.CreateSubscription("test-topic");

            // Assert
            Assert.NotNull(subscription);
            Assert.IsAssignableFrom<IEventGridSubscription>(subscription);
        }

        [Fact]
        public void CreateSubscription_Should_Return_Different_Instances_For_Different_Topics()
        {
            // Arrange
            var settings = new AzureEventGridSettings
            {
                TopicEndpoint = "https://test.eventgrid.azure.net/api/events",
                AccessKey = "test-key",
                WebhookEndpoint = "https://test-webhook.com/events"
            };
            var options = Options.Create(settings);
            var provider = new EventGridProvider(options);

            // Act
            var subscription1 = provider.CreateSubscription("topic1");
            var subscription2 = provider.CreateSubscription("topic2");

            // Assert
            Assert.NotSame(subscription1, subscription2);
        }
    }
}