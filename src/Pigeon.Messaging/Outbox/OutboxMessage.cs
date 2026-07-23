namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Represents a fully prepared message that should be delivered to a broker later.
    /// </summary>
    public class OutboxMessage
    {
        /// <summary>
        /// Gets or sets the unique outbox message id.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets the serialized payload that will be sent to the broker.
        /// </summary>
        public string Payload { get; set; }

        /// <summary>
        /// Gets or sets the assembly-qualified CLR type name used to deserialize the payload.
        /// </summary>
        public string PayloadType { get; set; }

        /// <summary>
        /// Gets or sets whether the payload should be published without the Pigeon wrapper.
        /// </summary>
        public bool IsRaw { get; set; }

        /// <summary>
        /// Gets or sets the logical topic for topic-based publishing.
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// Gets or sets the broker exchange for routed publishing.
        /// </summary>
        public string Exchange { get; set; }

        /// <summary>
        /// Gets or sets the broker routing key for routed publishing.
        /// </summary>
        public string RoutingKey { get; set; }

        /// <summary>
        /// Gets or sets the current outbox lifecycle status.
        /// </summary>
        public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

        /// <summary>
        /// Gets or sets the number of failed publish attempts.
        /// </summary>
        public int Attempts { get; set; }

        /// <summary>
        /// Gets or sets the last publish failure details.
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the message was persisted.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when the message was locked for dispatch.
        /// </summary>
        public DateTimeOffset? LockedOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the message can be retried.
        /// </summary>
        public DateTimeOffset? NextAttemptOnUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when the message was published successfully.
        /// </summary>
        public DateTimeOffset? PublishedOnUtc { get; set; }
    }
}
