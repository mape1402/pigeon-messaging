namespace Pigeon.Messaging.Consuming.Configuration
{
    /// <summary>
    /// Represents a broker consumer endpoint for a topic and subscription.
    /// </summary>
    public sealed class ConsumerEndpoint
    {
        /// <summary>
        /// Default subscription used when none is specified.
        /// </summary>
        public const string DefaultSubscription = "Default";

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerEndpoint"/> class.
        /// </summary>
        public ConsumerEndpoint(string topic, string subscription = DefaultSubscription)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            Topic = topic;
            Subscription = string.IsNullOrWhiteSpace(subscription) ? DefaultSubscription : subscription;
        }

        /// <summary>
        /// Gets the topic consumed by this endpoint.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the subscription, queue name, or consumer group used by this endpoint.
        /// </summary>
        public string Subscription { get; }

        /// <summary>
        /// Gets a stable key for dictionaries and broker resources.
        /// </summary>
        public string Key => $"{Topic}::{Subscription}";

        /// <summary>
        /// Gets the default broker resource name for this endpoint.
        /// </summary>
        public string ResourceName => Subscription == DefaultSubscription ? Topic : Subscription;
    }
}
