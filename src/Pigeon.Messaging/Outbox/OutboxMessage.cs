namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Represents a fully prepared message that should be delivered to a broker later.
    /// </summary>
    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Payload { get; set; }

        public string PayloadType { get; set; }

        public bool IsRaw { get; set; }

        public string Topic { get; set; }

        public string Exchange { get; set; }

        public string RoutingKey { get; set; }

        public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

        public int Attempts { get; set; }

        public string LastError { get; set; }

        public DateTimeOffset CreatedOnUtc { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? LockedOnUtc { get; set; }

        public DateTimeOffset? NextAttemptOnUtc { get; set; }

        public DateTimeOffset? PublishedOnUtc { get; set; }
    }
}
