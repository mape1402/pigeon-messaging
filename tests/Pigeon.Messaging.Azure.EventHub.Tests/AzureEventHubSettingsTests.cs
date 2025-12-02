using Pigeon.Messaging.Azure.EventHub;

namespace Pigeon.Messaging.Azure.EventHub.Tests
{
    public class AzureEventHubSettingsTests
    {
        [Fact]
        public void Properties_Should_GetSet_Correctly()
        {
            // Arrange
            var settings = new AzureEventHubSettings();
            var connectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test-key";
            var consumerGroup = "custom-consumer-group";
            var blobStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test-key;EndpointSuffix=core.windows.net";
            var blobContainerName = "custom-checkpoints";

            // Act
            settings.ConnectionString = connectionString;
            settings.ConsumerGroup = consumerGroup;
            settings.BlobStorageConnectionString = blobStorageConnectionString;
            settings.BlobContainerName = blobContainerName;

            // Assert
            Assert.Equal(connectionString, settings.ConnectionString);
            Assert.Equal(consumerGroup, settings.ConsumerGroup);
            Assert.Equal(blobStorageConnectionString, settings.BlobStorageConnectionString);
            Assert.Equal(blobContainerName, settings.BlobContainerName);
        }

        [Fact]
        public void Default_Constructor_Should_Initialize_With_Default_Values()
        {
            // Act
            var settings = new AzureEventHubSettings();

            // Assert
            Assert.Null(settings.ConnectionString);
            Assert.Equal("$Default", settings.ConsumerGroup);
            Assert.Null(settings.BlobStorageConnectionString);
            Assert.Equal("eventhub-checkpoints", settings.BlobContainerName);
        }

        [Fact]
        public void ConsumerGroup_Should_Have_Default_Value()
        {
            // Act
            var settings = new AzureEventHubSettings();

            // Assert
            Assert.Equal("$Default", settings.ConsumerGroup);
        }

        [Fact]
        public void BlobContainerName_Should_Have_Default_Value()
        {
            // Act
            var settings = new AzureEventHubSettings();

            // Assert
            Assert.Equal("eventhub-checkpoints", settings.BlobContainerName);
        }
    }
}