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
        /// Gets the number of actions completed by Mule's runtime.
        /// </summary>
        public int RuntimeCompletedMessages { get; init; }

        /// <summary>
        /// Gets the number of actions failed by Mule's runtime.
        /// </summary>
        public int RuntimeFailedMessages { get; init; }

        /// <summary>
        /// Gets the current throughput reported by Mule for the last minute.
        /// </summary>
        public int ThroughputPerMinute { get; init; }

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

        /// <summary>
        /// Gets the average time between enqueue and execution start.
        /// </summary>
        public TimeSpan? AverageEnqueueToDispatchLatency { get; init; }

        /// <summary>
        /// Gets the average dispatch execution time.
        /// </summary>
        public TimeSpan? AverageDispatchToPublishedLatency { get; init; }

        /// <summary>
        /// Gets the pending backlog by Mule lane.
        /// </summary>
        public IReadOnlyDictionary<string, int> BacklogByLane { get; init; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets the completed messages per minute by Mule lane.
        /// </summary>
        public IReadOnlyDictionary<string, int> CompletedPerMinuteByLane { get; init; } = new Dictionary<string, int>();

        /// <summary>
        /// Gets the runtime failures by Mule lane.
        /// </summary>
        public IReadOnlyDictionary<string, int> RuntimeFailedByLane { get; init; } = new Dictionary<string, int>();
    }
}
