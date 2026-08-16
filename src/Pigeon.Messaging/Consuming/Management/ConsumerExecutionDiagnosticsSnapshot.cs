namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Represents a point-in-time view of consumer execution state.
    /// </summary>
    public sealed class ConsumerExecutionDiagnosticsSnapshot
    {
        /// <summary>
        /// Gets the number of messages received from broker adapters.
        /// </summary>
        public long ReceivedMessages { get; init; }

        /// <summary>
        /// Gets the number of messages waiting in Pigeon's internal dispatch queue.
        /// </summary>
        public long QueuedMessages { get; init; }

        /// <summary>
        /// Gets the number of handlers currently running.
        /// </summary>
        public long ActiveHandlers { get; init; }

        /// <summary>
        /// Gets the number of handlers that completed successfully.
        /// </summary>
        public long CompletedHandlers { get; init; }

        /// <summary>
        /// Gets the number of handlers that failed.
        /// </summary>
        public long FailedHandlers { get; init; }

        /// <summary>
        /// Gets the number of completed acknowledgements.
        /// </summary>
        public long AcknowledgedMessages { get; init; }

        /// <summary>
        /// Gets the number of rejected acknowledgements.
        /// </summary>
        public long RejectedMessages { get; init; }

        /// <summary>
        /// Gets the average time a message waited in the internal dispatch queue.
        /// </summary>
        public TimeSpan AverageQueueWait { get; init; }

        /// <summary>
        /// Gets whether the internal dispatch queue is bounded.
        /// </summary>
        public bool IsQueueBounded { get; init; }

        /// <summary>
        /// Gets the configured queue capacity when bounded.
        /// </summary>
        public int? QueueCapacity { get; init; }

        /// <summary>
        /// Gets the configured maximum handler concurrency when bounded by Pigeon.
        /// </summary>
        public int? MaxConcurrency { get; init; }

        /// <summary>
        /// Gets the configured broker prefetch count when available.
        /// </summary>
        public ushort? PrefetchCount { get; init; }
    }
}
