namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Contains information about a consumed raw message, including its topic and payload.
    /// </summary>
    public class MessageConsumedEventArgs : EventArgs
    {
        /// <summary>
        /// The topic or channel from which the message was consumed.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// The raw message payload, typically as a JSON string.
        /// </summary>
        public string RawPayload { get; }

        /// <summary>
        /// The subscription, queue name, or consumer group that consumed the message.
        /// </summary>
        public string Subscription { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsumedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload of the message.</param>
        public MessageConsumedEventArgs(string topic, string rawPayload)
            : this(topic, rawPayload, Configuration.ConsumerEndpoint.DefaultSubscription)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsumedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload of the message.</param>
        /// <param name="subscription">The subscription that consumed the message.</param>
        public MessageConsumedEventArgs(string topic, string rawPayload, string subscription)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            RawPayload = rawPayload ?? throw new ArgumentNullException(nameof(rawPayload));
            Subscription = string.IsNullOrWhiteSpace(subscription) ? Configuration.ConsumerEndpoint.DefaultSubscription : subscription;
        }
    }
}
