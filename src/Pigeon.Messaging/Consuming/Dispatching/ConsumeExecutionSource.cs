namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Identifies where a consume invocation originated.
    /// </summary>
    public enum ConsumeExecutionSource
    {
        /// <summary>
        /// The message was received from a live broker delivery.
        /// </summary>
        BrokerDelivery = 0,

        /// <summary>
        /// The message was replayed from a persisted consume envelope.
        /// </summary>
        DeferredReplay = 1,

        /// <summary>
        /// The message was invoked manually, usually by tests or application code.
        /// </summary>
        Manual = 2
    }
}
