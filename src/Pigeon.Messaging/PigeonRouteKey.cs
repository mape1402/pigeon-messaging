namespace Pigeon.Messaging
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Identifies a logical Pigeon route using topic, version, and subscription.
    /// </summary>
    public readonly record struct PigeonRouteKey
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PigeonRouteKey"/> struct.
        /// </summary>
        /// <param name="topic">The logical topic name.</param>
        /// <param name="version">The semantic contract version.</param>
        /// <param name="subscription">The optional subscription, queue name, or consumer group.</param>
        public PigeonRouteKey(string topic, SemanticVersion version, string subscription = null)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            Topic = topic;
            Version = version;
            Subscription = string.IsNullOrWhiteSpace(subscription)
                ? ConsumerEndpoint.DefaultSubscription
                : subscription;
        }

        /// <summary>
        /// Gets the logical topic name.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the semantic contract version.
        /// </summary>
        public SemanticVersion Version { get; }

        /// <summary>
        /// Gets the subscription, queue name, or consumer group.
        /// </summary>
        public string Subscription { get; }
    }
}
