namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Coordinates immediate outbox dispatch with the current transactional boundary.
    /// </summary>
    public interface IOutboxCommitNotifier
    {
        /// <summary>
        /// Notifies Pigeon that an outbox message was saved and can be dispatched after commit.
        /// </summary>
        /// <param name="outboxMessageId">The saved outbox message id.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been queued or commit observation has been registered.</returns>
        ValueTask NotifySavedAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);
    }
}
