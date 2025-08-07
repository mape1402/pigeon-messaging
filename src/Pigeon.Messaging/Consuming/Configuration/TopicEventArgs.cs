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
        public TopicEventArgs(string topic)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
        }

        /// <summary>
        /// Gets the topic that was created.
        /// </summary>
        public string Topic { get; }
    }
}
