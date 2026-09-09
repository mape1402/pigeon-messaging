namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Represents a transport-neutral consumed message envelope that can be persisted and replayed.
    /// </summary>
    public sealed class PigeonConsumeEnvelope
    {
        /// <summary>
        /// Gets the optional message identifier.
        /// </summary>
        public string MessageId { get; init; }

        /// <summary>
        /// Gets the optional correlation identifier.
        /// </summary>
        public string CorrelationId { get; init; }

        /// <summary>
        /// Gets the logical topic.
        /// </summary>
        public string Topic { get; init; }

        /// <summary>
        /// Gets the optional logical operation.
        /// </summary>
        public string Operation { get; init; }

        /// <summary>
        /// Gets the semantic contract version.
        /// </summary>
        public SemanticVersion Version { get; init; }

        /// <summary>
        /// Gets the subscription, queue name, or consumer group.
        /// </summary>
        public string Subscription { get; init; }

        /// <summary>
        /// Gets the assembly-qualified payload type name captured at consume time.
        /// </summary>
        public string PayloadType { get; init; }

        /// <summary>
        /// Gets the serialized business payload.
        /// </summary>
        public byte[] Payload { get; init; }

        /// <summary>
        /// Gets the payload content type.
        /// </summary>
        public string ContentType { get; init; } = "application/json";

        /// <summary>
        /// Gets the original message creation timestamp.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; init; }

        /// <summary>
        /// Gets the optional logical source domain.
        /// </summary>
        public string From { get; init; }

        /// <summary>
        /// Gets transport headers attached to the delivery.
        /// </summary>
        public Dictionary<string, string> Headers { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets metadata attached to the message envelope.
        /// </summary>
        public Dictionary<string, string> Metadata { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
