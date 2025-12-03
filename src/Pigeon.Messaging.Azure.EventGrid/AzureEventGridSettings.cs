namespace Pigeon.Messaging.Azure.EventGrid
{
    /// <summary>
    /// Represents the configuration settings required to connect to Azure Event Grid.
    /// </summary>
    public class AzureEventGridSettings
    {
        /// <summary>
        /// Gets or sets the mapping of topics to their corresponding routing keys.
        /// </summary>
        public Dictionary<string, string> TopicRouting { get; set; }

        /// <summary>
        /// Gets or sets the collection of endpoints, where each endpoint is identified by a unique key.
        /// </summary>
        /// <remarks>Use this property to manage and access the available endpoints in the application.
        /// The keys in the dictionary must be unique and are typically used to identify specific endpoints. Modifying
        /// this property will replace the entire collection of endpoints.</remarks>
        public Dictionary<string, Endpoint> Endpoints { get; set; }

        /// <summary>
        /// Gets or sets the endpoint URI for the Service Bus.
        /// </summary>
        public string ServiceBusEndPoint { get; set; }

        /// <summary>
        /// Gets or sets the default endpoint used for connecting to the service.
        /// </summary>
        public string DefaultEndpoint { get; set; }
    }

    /// <summary>
    /// Represents an Event Grid endpoint, including its URL and authentication access key.
    /// </summary>
    /// <remarks>This class is used to configure the connection to an Event Grid topic.  The <see cref="Url"/>
    /// property specifies the endpoint URL, and the <see cref="AccessKey"/>  property provides the authentication key
    /// required to interact with the Event Grid service.</remarks>
    public class Endpoint
    {
        /// <summary>
        /// Gets or sets the Event Grid topic endpoint URL.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the access key for Event Grid authentication.
        /// </summary>
        public string AccessKey { get; set; }
    }
}