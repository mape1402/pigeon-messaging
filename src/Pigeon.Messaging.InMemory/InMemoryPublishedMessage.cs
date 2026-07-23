namespace Pigeon.Messaging.InMemory
{
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Represents a message published through the in-memory broker.
    /// </summary>
    public sealed class InMemoryPublishedMessage
    {
        /// <summary>
        /// Gets or sets the unique in-memory message id.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the serialized payload.
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// Gets or sets the route used to publish the message.
        /// </summary>
        public PublishingRoute Route { get; set; }

        /// <summary>
        /// Gets or sets whether the payload was published without a Pigeon wrapper.
        /// </summary>
        public bool IsRaw { get; set; }

        /// <summary>
        /// Gets or sets when the message was published.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; set; }
    }
}
