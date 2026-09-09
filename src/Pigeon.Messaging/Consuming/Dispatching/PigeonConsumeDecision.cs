namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Represents a decision produced before a message reaches its consumer handler.
    /// </summary>
    public enum PigeonConsumeDecision
    {
        /// <summary>
        /// Continue through the consumer pipeline and execute the handler.
        /// </summary>
        Continue = 0,

        /// <summary>
        /// Skip the handler and acknowledge the broker delivery.
        /// </summary>
        AckAndSkip = 1,

        /// <summary>
        /// Skip the handler and reject the broker delivery.
        /// </summary>
        Reject = 2,

        /// <summary>
        /// Skip the handler and request broker retry semantics when available.
        /// </summary>
        Retry = 3,

        /// <summary>
        /// Skip the handler because durable work has been scheduled by the interceptor.
        /// </summary>
        Defer = 4
    }
}
