namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Represents a point-in-time view of outbox message state.
    /// </summary>
    public sealed class OutboxDiagnosticsSnapshot
    {
        /// <summary>
        /// Gets the number of messages waiting to be dispatched.
        /// </summary>
        public int PendingMessages { get; init; }

        /// <summary>
        /// Gets the number of messages currently locked by a dispatcher.
        /// </summary>
        public int LockedMessages { get; init; }

        /// <summary>
        /// Gets the number of messages published successfully.
        /// </summary>
        public int PublishedMessages { get; init; }

        /// <summary>
        /// Gets the number of messages that exhausted retry attempts.
        /// </summary>
        public int FailedMessages { get; init; }

        /// <summary>
        /// Gets the oldest pending message creation timestamp.
        /// </summary>
        public DateTimeOffset? OldestPendingMessageOnUtc { get; init; }

        /// <summary>
        /// Gets the oldest failed message creation timestamp.
        /// </summary>
        public DateTimeOffset? OldestFailedMessageOnUtc { get; init; }

        /// <summary>
        /// Gets the most recent failure details, when available.
        /// </summary>
        public string LastFailure { get; init; }
    }
}
