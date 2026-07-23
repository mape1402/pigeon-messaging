namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Represents the lifecycle state of a persisted outbox message.
    /// </summary>
    public enum OutboxMessageStatus
    {
        /// <summary>
        /// The message is waiting to be published.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// The message is locked by a dispatcher.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// The message was published successfully.
        /// </summary>
        Published = 2,

        /// <summary>
        /// The message exhausted its retry attempts.
        /// </summary>
        Failed = 3
    }
}
