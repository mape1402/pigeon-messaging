namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Coordinates immediate outbox dispatch with the current transactional boundary.
    /// </summary>
    public interface IOutboxCommitNotifier
    {
        ValueTask NotifySavedAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);
    }
}
