namespace Pigeon.Messaging
{
    /// <summary>
    /// Represents the configuration settings for messaging,
    /// including broker URL and domain information.
    /// </summary>
    public class MessagingOptions
    {
        /// <summary>
        /// Gets or sets the connection URL of the messaging broker
        /// (e.g., Kafka broker address, RabbitMQ endpoint, etc.).
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the domain name that identifies the logical scope
        /// or boundary for the published messages.
        /// This can be used to categorize or route messages within a distributed system.
        /// </summary>
        public string Domain { get; set; }
    }
}
