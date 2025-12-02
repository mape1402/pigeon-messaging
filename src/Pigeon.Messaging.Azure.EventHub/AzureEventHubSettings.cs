namespace Pigeon.Messaging.Azure.EventHub
{
    /// <summary>
    /// Represents the configuration settings required to connect to Azure Event Hubs.
    /// </summary>
    public class AzureEventHubSettings
    {
        /// <summary>
        /// Gets or sets the Event Hubs namespace connection string.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the consumer group name for receiving events.
        /// </summary>
        public string ConsumerGroup { get; set; } = "$Default";

        /// <summary>
        /// Gets or sets the blob storage connection string for checkpoint storage.
        /// </summary>
        public string BlobStorageConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the blob container name for checkpoint storage.
        /// </summary>
        public string BlobContainerName { get; set; } = "eventhub-checkpoints";
    }
}