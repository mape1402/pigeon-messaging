namespace Pigeon.Messaging
{
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Represents the configuration settings for messaging,
    /// including broker URL and domain information.
    /// </summary>
    public class MessagingSettings
    {
        /// <summary>
        /// Gets or sets the domain name that identifies the logical scope
        /// or boundary for the published messages.
        /// This can be used to categorize or route messages within a distributed system.
        /// </summary>
        public string Domain { get; init; }

        /// <summary>
        /// Gets or sets the configuration sections for each message broker.
        /// Each broker can have its own nested and strongly-typed options.
        /// </summary>
        public Dictionary<string, IConfigurationSection> MessageBrokers { get; init; }
    }
}
