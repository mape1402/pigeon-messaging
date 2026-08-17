using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Pigeon.Messaging.Consuming.Management;
using System.Reflection;

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

        [Fact]
        public void CreateProcessorOptions_Should_Map_ConsumerExecution_Throughput_Settings()
        {
            var options = Options.Create(new AzureServiceBusSettings { ConnectionString = "Endpoint=sb://test/;SharedAccessKeyName=Root;SharedAccessKey=abc" });
            var globalSettings = Options.Create(new GlobalSettings
            {
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    AcknowledgementMode = MessageAcknowledgementMode.OnReceive,
                    MaxConcurrency = 32,
                    PrefetchCount = 128
                }
            });
            var provider = new ServiceBusProvider(options, globalSettings);

            var processorOptions = (ServiceBusProcessorOptions)typeof(ServiceBusProvider)
                .GetMethod("CreateProcessorOptions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(provider, []);

            Assert.True(processorOptions.AutoCompleteMessages);
            Assert.Equal(32, processorOptions.MaxConcurrentCalls);
            Assert.Equal(128, processorOptions.PrefetchCount);
        }
    }
}
