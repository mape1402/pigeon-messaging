namespace Pigeon.Messaging.InMemory
{
    /// <summary>
    /// Represents one in-memory delivery attempt to a consumer subscription.
    /// </summary>
    public sealed class InMemoryDelivery
    {
        /// <summary>
        /// Gets or sets the related published message id.
        /// </summary>
        public Guid MessageId { get; set; }

        /// <summary>
        /// Gets or sets the topic delivered to the consumer pipeline.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Gets or sets the subscription that received the message.
        /// </summary>
        public string Subscription { get; set; }

        /// <summary>
        /// Gets or sets whether the delivery was acknowledged.
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// Gets or sets whether the delivery was failed.
        /// </summary>
        public bool Failed { get; set; }

        /// <summary>
        /// Gets or sets the last failure message.
        /// </summary>
        public string Error { get; set; }
    }
}
