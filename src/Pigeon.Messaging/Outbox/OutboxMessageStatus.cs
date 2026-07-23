namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Represents the lifecycle state of a persisted outbox message.
    /// </summary>
    public enum OutboxMessageStatus
    {
        Pending = 0,
        Locked = 1,
        Published = 2,
        Failed = 3
    }
}
