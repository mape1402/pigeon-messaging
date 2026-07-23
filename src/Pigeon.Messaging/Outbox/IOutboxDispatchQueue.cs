namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// In-memory dispatch queue used to trigger outbox delivery immediately after a successful commit.
    /// </summary>
    public interface IOutboxDispatchQueue
    {
        /// <summary>
        /// Enqueues an outbox message id for immediate background dispatch.
        /// </summary>
        /// <param name="outboxMessageId">The outbox message id.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the id has been queued.</returns>
        ValueTask EnqueueAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dequeues the next outbox message id to dispatch.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>The next outbox message id.</returns>
        ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
    }
}
