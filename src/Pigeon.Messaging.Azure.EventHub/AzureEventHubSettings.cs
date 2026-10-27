namespace Pigeon.Messaging.Azure.EventHub
{
    using global::Azure.Core;

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
        /// Gets or sets the fully qualified Event Hubs namespace used with <see cref="Credential"/>.
        /// </summary>
        public string FullyQualifiedNamespace { get; set; }

        /// <summary>
        /// Gets or sets the programmatically supplied Microsoft Entra credential.
        /// </summary>
        /// <remarks>
        /// This property cannot be bound from application configuration. Configure it in code.
        /// </remarks>
        public TokenCredential Credential { get; set; }

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
