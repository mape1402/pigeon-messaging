namespace Pigeon.Messaging.Azure.EventGrid.Tests
{
    public class SimpleTests
    {
        [Fact]
        public void AzureEventGridSettings_Should_Store_Routing_And_Endpoint_Configuration()
        {
            var settings = new AzureEventGridSettings
            {
                DefaultEndpoint = "default",
                TopicRouting = new Dictionary<string, string>
                {
                    ["orders.created"] = "orders"
                },
                Endpoints = new Dictionary<string, Endpoint>
                {
                    ["orders"] = new()
                    {
                        Url = "https://orders.example.test/api/events",
                        AccessKey = "secret"
                    }
                }
            };

            Assert.Equal("default", settings.DefaultEndpoint);
            Assert.Equal("orders", settings.TopicRouting["orders.created"]);
            Assert.Equal("https://orders.example.test/api/events", settings.Endpoints["orders"].Url);
            Assert.Equal("secret", settings.Endpoints["orders"].AccessKey);
        }
    }
}
