namespace Pigeon.Messaging.Azure.EventGrid
{
    /// <summary>
    /// Represents the configuration settings required to connect to Azure Event Grid.
    /// </summary>
    public class AzureEventGridSettings
    {
        /// <summary>
        /// Gets or sets the Event Grid topic endpoint URL.
        /// </summary>
        public string TopicEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the access key for Event Grid authentication.
        /// </summary>
        public string AccessKey { get; set; }

        /// <summary>
        /// Gets or sets the endpoint URI for the Service Bus.
        /// </summary>
        public string ServiceBusEndPoint { get; set; }
    }
}