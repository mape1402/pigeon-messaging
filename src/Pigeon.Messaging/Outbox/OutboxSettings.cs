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
    }
}
