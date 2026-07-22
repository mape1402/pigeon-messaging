namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Represents the broker route used to publish a message.
    /// </summary>
    public sealed class PublishingRoute
    {
        private PublishingRoute(string topic, string exchange, string routingKey)
        {
            Topic = topic;
            Exchange = exchange;
            RoutingKey = routingKey;
        }

        /// <summary>
        /// Gets the logical topic used by Pigeon consumers.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the broker exchange or equivalent routing channel.
        /// </summary>
        public string Exchange { get; }

        /// <summary>
        /// Gets the broker routing key.
        /// </summary>
        public string RoutingKey { get; }

        /// <summary>
        /// Creates a route for the topic-only publish flow.
        /// </summary>
        public static PublishingRoute ForTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            return new PublishingRoute(topic, string.Empty, topic);
        }

        /// <summary>
        /// Creates a route that publishes to an exchange using a routing key.
        /// </summary>
        public static PublishingRoute ForExchange(string exchange, string routingKey)
        {
            if (string.IsNullOrWhiteSpace(exchange))
                throw new ArgumentException("Exchange cannot be null or empty.", nameof(exchange));

            if (string.IsNullOrWhiteSpace(routingKey))
                throw new ArgumentException("Routing key cannot be null or empty.", nameof(routingKey));

            return new PublishingRoute(routingKey, exchange, routingKey);
        }
    }
}
