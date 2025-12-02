using Pigeon.Messaging.Azure.EventGrid;

namespace Pigeon.Messaging.Azure.EventGrid.Tests
{
    public class AzureEventGridSettingsTests
    {
        [Fact]
        public void Properties_Should_GetSet_Correctly()
        {
            // Arrange
            var settings = new AzureEventGridSettings();
            var topicEndpoint = "https://test.eventgrid.azure.net/api/events";
            var accessKey = "test-access-key";
            var webhookEndpoint = "https://test-webhook.com/events";

            // Act
            settings.TopicEndpoint = topicEndpoint;
            settings.AccessKey = accessKey;
            settings.WebhookEndpoint = webhookEndpoint;

            // Assert
            Assert.Equal(topicEndpoint, settings.TopicEndpoint);
            Assert.Equal(accessKey, settings.AccessKey);
            Assert.Equal(webhookEndpoint, settings.WebhookEndpoint);
        }

        [Fact]
        public void Default_Constructor_Should_Initialize_With_Null_Properties()
        {
            // Act
            var settings = new AzureEventGridSettings();

            // Assert
            Assert.Null(settings.TopicEndpoint);
            Assert.Null(settings.AccessKey);
            Assert.Null(settings.WebhookEndpoint);
        }
    }
}