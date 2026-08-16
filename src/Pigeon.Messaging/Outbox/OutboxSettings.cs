namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Global settings for the transactional outbox pipeline.
    /// </summary>
    public sealed class OutboxSettings
    {
        /// <summary>
        /// Gets or sets whether producer calls should be persisted to the outbox instead of published immediately.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the interval used by the dispatcher to query pending outbox messages.
        /// </summary>
        public TimeSpan DispatchInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets whether committed outbox messages should be queued for immediate background dispatch.
        /// </summary>
        public bool ImmediateDispatch { get; set; } = true;

        /// <summary>
        /// Gets or sets the in-memory dispatch queue capacity. A value less than or equal to zero uses an unbounded queue.
        /// </summary>
        public int DispatchQueueCapacity { get; set; }

        /// <summary>
        /// Gets or sets the interval used to clean already published messages.
        /// </summary>
        public TimeSpan CleanInterval { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets or sets how long published messages should be retained before cleanup.
        /// </summary>
        public TimeSpan PublishedMessageRetention { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Gets or sets the maximum number of messages dispatched in one batch.
        /// </summary>
        public int DispatchBatchSize { get; set; } = 50;

        /// <summary>
        /// Gets or sets the number of Mule workers that drain the outbox dispatch queue.
        /// A value less than or equal to zero keeps Mule's default.
        /// </summary>
        public int WorkerCount { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of outbox actions Mule can execute concurrently.
        /// A value less than or equal to zero keeps Mule's default.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of storage batches Mule drains per recovery cycle.
        /// A value less than or equal to zero keeps Mule's default.
        /// </summary>
        public int MaxDrainBatchesPerCycle { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of actions Mule drains per recovery cycle.
        /// A value less than or equal to zero keeps Mule's default.
        /// </summary>
        public int MaxDrainActionsPerCycle { get; set; }

        /// <summary>
        /// Gets or sets whether Mule should keep draining ready actions until the outbox is empty.
        /// </summary>
        public bool DrainUntilEmpty { get; set; }

        /// <summary>
        /// Gets or sets the delay Mule yields between recovery drain batches.
        /// </summary>
        public TimeSpan YieldBetweenDrainBatches { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of published messages deleted in one cleanup batch.
        /// </summary>
        public int CleanBatchSize { get; set; } = 500;

        /// <summary>
        /// Gets or sets the maximum publish attempts before a message stays failed.
        /// </summary>
        public int MaxRetries { get; set; } = 10;

        /// <summary>
        /// Gets or sets the delay applied after a failed publish attempt.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets how long a locked message can stay locked before it can be retried.
        /// </summary>
        public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets how the selected provider should manage schema.
        /// </summary>
        public OutboxSchemaMode SchemaMode { get; set; } = OutboxSchemaMode.AutoCreate;

        /// <summary>
        /// Gets the optional Mule lane configuration keyed by lane name.
        /// </summary>
        public IDictionary<string, OutboxLaneSettings> Lanes { get; set; } = new Dictionary<string, OutboxLaneSettings>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Applies productive high-throughput defaults for Mule-backed outbox providers.
        /// </summary>
        /// <returns>The same settings instance for chaining.</returns>
        public OutboxSettings ConfigureHighThroughput()
        {
            WorkerCount = Math.Max(WorkerCount, Environment.ProcessorCount);
            MaxDegreeOfParallelism = Math.Max(MaxDegreeOfParallelism, Environment.ProcessorCount * 8);
            DispatchBatchSize = Math.Max(DispatchBatchSize, 250);
            MaxDrainBatchesPerCycle = Math.Max(MaxDrainBatchesPerCycle, 8);
            MaxDrainActionsPerCycle = Math.Max(MaxDrainActionsPerCycle, 2_000);
            YieldBetweenDrainBatches = YieldBetweenDrainBatches <= TimeSpan.Zero
                ? TimeSpan.FromMilliseconds(1)
                : YieldBetweenDrainBatches;
            DispatchInterval = DispatchInterval <= TimeSpan.Zero || DispatchInterval > TimeSpan.FromSeconds(5)
                ? TimeSpan.FromSeconds(5)
                : DispatchInterval;
            ImmediateDispatch = true;

            return this;
        }
    }
}
