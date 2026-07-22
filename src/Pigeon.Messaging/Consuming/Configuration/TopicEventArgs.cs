namespace Pigeon.Messaging.Consuming.Configuration
{
    /// <summary>
    /// Represents the event arguments for the <see cref="IConsumingConfigurator.TopicCreated"/> event.
    /// </summary>
    public class TopicEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TopicEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic that was created.</param>
        public TopicEventArgs(string topic) : this(topic, ConsumerEndpoint.DefaultSubscription)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TopicEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic that was created.</param>
        /// <param name="subscription">The subscription that was created.</param>
        public TopicEventArgs(string topic, string subscription)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            Subscription = string.IsNullOrWhiteSpace(subscription) ? ConsumerEndpoint.DefaultSubscription : subscription;
        }

        /// <summary>
        /// Gets the topic that was created.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the subscription that was created.
        /// </summary>
        public string Subscription { get; }

        /// <summary>
        /// Gets the endpoint represented by this event.
        /// </summary>
        public ConsumerEndpoint Endpoint => new ConsumerEndpoint(Topic, Subscription);
    }
}
