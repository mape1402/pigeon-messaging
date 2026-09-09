namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Represents a decision produced before a message is sent to the broker or outbox.
    /// </summary>
    public enum PigeonPublishDecision
    {
        /// <summary>
        /// Continue with the configured publish behavior.
        /// </summary>
        Continue = 0,

        /// <summary>
        /// Skip publishing without treating the operation as a failure.
        /// </summary>
        Skip = 1,

        /// <summary>
        /// Reject the publish operation.
        /// </summary>
        Reject = 2,

        /// <summary>
        /// Store the message in the outbox, even if direct publishing would otherwise be used.
        /// </summary>
        UseOutbox = 3,

        /// <summary>
        /// Publish directly to the broker, even if outbox is configured.
        /// </summary>
        PublishNow = 4
    }
}
