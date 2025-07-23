using Microsoft.Extensions.Options;

namespace Pigeon.Messaging.Azure.ServiceBus.Tests
{
    public class ServiceBusProviderTests
    {
        [Fact]
        public void GetClient_Returns_Same_Instance()
        {
            // Arrange
            var options = Options.Create(new AzureServiceBusSettings { ConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc" });
            var provider = new ServiceBusProvider(options);

            // Act
            var client1 = provider.GetClient();
            var client2 = provider.GetClient();

            // Assert
            Assert.Same(client1, client2);
        }

        [Fact]
        public void GetSender_Returns_Same_Instance_For_Same_Topic()
        {
            // Arrange
            var options = Options.Create(new AzureServiceBusSettings { ConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc" });
            var provider = new ServiceBusProvider(options);
            var topic = "topic1";

            // Act
            var sender1 = provider.GetSender(topic);
            var sender2 = provider.GetSender(topic);

            // Assert
            Assert.Same(sender1, sender2);
        }
    }
}
