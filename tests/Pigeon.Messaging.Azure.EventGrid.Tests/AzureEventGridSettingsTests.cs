namespace Pigeon.Messaging.Azure.EventGrid.Tests
{
    public class AzureEventGridSettingsTests
    {
        [Fact]
        public void Properties_Should_GetSet_Correctly()
        {
            // Arrange
            var settings = new AzureEventGridSettings();
            var serviceBusEndPoint = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=testkey";
            var topicRouting = new Dictionary<string, string>
            {
                ["topic1"] = "endpoint1",
                ["topic2"] = "endpoint2"
            };
            var endpoints = new Dictionary<string, Endpoint>
            {
                ["endpoint1"] = new Endpoint
                {
                    Url = "https://test1.eventgrid.azure.net/api/events",
                    AccessKey = "test-key-1"
                },
                ["endpoint2"] = new Endpoint
                {
                    Url = "https://test2.eventgrid.azure.net/api/events", 
                    AccessKey = "test-key-2"
                }
            };

            // Act
            settings.ServiceBusEndPoint = serviceBusEndPoint;
            settings.TopicRouting = topicRouting;
            settings.Endpoints = endpoints;

            // Assert
            Assert.Equal(serviceBusEndPoint, settings.ServiceBusEndPoint);
            Assert.Equal(topicRouting, settings.TopicRouting);
            Assert.Equal(endpoints, settings.Endpoints);
        }

        [Fact]
        public void Default_Constructor_Should_Initialize_With_Null_Properties()
        {
            // Act
            var settings = new AzureEventGridSettings();

            // Assert
            Assert.Null(settings.ServiceBusEndPoint);
            Assert.Null(settings.TopicRouting);
            Assert.Null(settings.Endpoints);
        }

        [Fact]
        public void Endpoint_Properties_Should_GetSet_Correctly()
        {
            // Arrange
            var endpoint = new Endpoint();
            var url = "https://test.eventgrid.azure.net/api/events";
            var accessKey = "test-access-key";

            // Act
            endpoint.Url = url;
            endpoint.AccessKey = accessKey;

            // Assert
            Assert.Equal(url, endpoint.Url);
            Assert.Equal(accessKey, endpoint.AccessKey);
        }
    }
}