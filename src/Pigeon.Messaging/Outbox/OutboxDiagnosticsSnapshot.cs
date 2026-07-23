namespace Pigeon.Messaging.Outbox
{
    public sealed class OutboxDiagnosticsSnapshot
    {
        public int PendingMessages { get; init; }

        public int LockedMessages { get; init; }

        public int PublishedMessages { get; init; }

        public int FailedMessages { get; init; }

        public DateTimeOffset? OldestPendingMessageOnUtc { get; init; }

        public DateTimeOffset? OldestFailedMessageOnUtc { get; init; }

        public string LastFailure { get; init; }
    }
}
