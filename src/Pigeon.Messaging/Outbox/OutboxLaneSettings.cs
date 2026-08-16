namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Configures a Mule lane used by the Pigeon outbox.
    /// </summary>
    public sealed class OutboxLaneSettings
    {
        /// <summary>
        /// Gets or sets the number of workers assigned to this lane.
        /// </summary>
        public int WorkerCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of actions this lane can execute concurrently.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Gets or sets the dispatch batch size for this lane.
        /// </summary>
        public int DispatchBatchSize { get; set; }

        /// <summary>
        /// Gets or sets the maximum drain batches per recovery cycle for this lane.
        /// </summary>
        public int MaxDrainBatchesPerCycle { get; set; }

        /// <summary>
        /// Gets or sets the maximum actions drained per recovery cycle for this lane.
        /// </summary>
        public int MaxDrainActionsPerCycle { get; set; }

        /// <summary>
        /// Gets or sets whether this lane drains until empty.
        /// </summary>
        public bool? DrainUntilEmpty { get; set; }

        /// <summary>
        /// Gets or sets the delay yielded between drain batches for this lane.
        /// </summary>
        public TimeSpan YieldBetweenDrainBatches { get; set; }

        /// <summary>
        /// Gets or sets the in-memory dispatch queue capacity for this lane.
        /// </summary>
        public int DispatchQueueCapacity { get; set; }

        /// <summary>
        /// Gets or sets the polling interval for this lane.
        /// </summary>
        public TimeSpan PollingInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum attempts for this lane.
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// Gets or sets the retry delay for this lane.
        /// </summary>
        public TimeSpan RetryDelay { get; set; }

        /// <summary>
        /// Gets or sets the lane priority.
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Gets or sets the lane weight.
        /// </summary>
        public int Weight { get; set; }
    }
}
