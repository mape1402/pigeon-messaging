namespace Pigeon.Messaging.Consuming.Management
{
    internal sealed record QueuedConsumedMessage(MessageConsumedEventArgs Message, DateTimeOffset EnqueuedOnUtc);
}
