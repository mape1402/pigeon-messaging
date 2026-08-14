namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Controls consumer dispatch execution.
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
        /// When null, Pigeon does not apply an internal concurrency limit and lets the broker or adapter drive delivery.
        /// </summary>
        public int? MaxConcurrency { get; set; }

        /// <summary>
        /// Gets or sets the in-memory queue capacity used before applying backpressure.
        /// When null, Pigeon uses an unbounded dispatch queue.
        /// </summary>
        public int? QueueCapacity { get; set; }

        /// <summary>
        /// Gets or sets the maximum time allowed for each handler dispatch.
        /// </summary>
        public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
