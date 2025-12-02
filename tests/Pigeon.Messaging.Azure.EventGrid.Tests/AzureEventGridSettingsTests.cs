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
            var serviceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey";

            // Act
            settings.TopicEndpoint = topicEndpoint;
            settings.AccessKey = accessKey;
            settings.ServiceBusEndPoint = serviceBusEndPoint;

            // Assert
            Assert.Equal(topicEndpoint, settings.TopicEndpoint);
            Assert.Equal(accessKey, settings.AccessKey);
            Assert.Equal(serviceBusEndPoint, settings.ServiceBusEndPoint);
        }

        [Fact]
        public void Default_Constructor_Should_Initialize_With_Null_Properties()
        {
            // Act
            var settings = new AzureEventGridSettings();

            // Assert
            Assert.Null(settings.TopicEndpoint);
            Assert.Null(settings.AccessKey);
            Assert.Null(settings.ServiceBusEndPoint);
        }
    }
}