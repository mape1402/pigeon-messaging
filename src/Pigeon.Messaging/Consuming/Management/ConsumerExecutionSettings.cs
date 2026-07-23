namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Controls bounded consumer dispatch concurrency.
    /// </summary>
    public sealed class ConsumerExecutionSettings
    {
        /// <summary>
        /// Gets or sets who acknowledges broker messages after dispatch.
        /// Defaults to <see cref="MessageAcknowledgementMode.Manual"/>.
        /// </summary>
        public MessageAcknowledgementMode AcknowledgementMode { get; set; } = MessageAcknowledgementMode.Manual;

        /// <summary>
        /// Gets or sets the maximum number of messages dispatched concurrently.
        /// </summary>
        public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets the in-memory queue capacity used before applying backpressure.
        /// </summary>
        public int QueueCapacity { get; set; } = 1_000;

        /// <summary>
        /// Gets or sets the maximum time allowed for each handler dispatch.
        /// </summary>
        public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
