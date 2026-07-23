namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Defines the persistence contract used by the Pigeon transactional outbox.
    /// </summary>
    public interface IOutboxStorage
    {
        /// <summary>
        /// Adds a prepared message to the outbox storage.
        /// </summary>
        /// <param name="message">The message to persist.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the message has been staged.</returns>
        Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Locks pending or expired locked messages so they can be queued for dispatch.
        /// </summary>
        /// <param name="batchSize">The maximum number of messages to lock.</param>
        /// <param name="lockTimeout">The amount of time after which a locked message can be recovered.</param>
        /// <param name="now">The current timestamp used for lock calculations.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>The messages eligible for dispatch.</returns>
        Task<IReadOnlyCollection<OutboxMessage>> LockPendingAsync(
            int batchSize,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Locks a single outbox message by id when it is eligible for dispatch.
        /// </summary>
        /// <param name="id">The outbox message id.</param>
        /// <param name="lockTimeout">The amount of time after which a locked message can be recovered.</param>
        /// <param name="now">The current timestamp used for lock calculations.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>The locked message, or null if it is not dispatchable.</returns>
        Task<OutboxMessage> LockAsync(
            Guid id,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a message as successfully published.
        /// </summary>
        /// <param name="id">The outbox message id.</param>
        /// <param name="publishedOnUtc">The UTC timestamp when the message was published.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the status update has been staged.</returns>
        Task MarkPublishedAsync(Guid id, DateTimeOffset publishedOnUtc, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks a message as failed or schedules it for retry.
        /// </summary>
        /// <param name="id">The outbox message id.</param>
        /// <param name="error">The failure details.</param>
        /// <param name="now">The current timestamp.</param>
        /// <param name="nextAttemptOnUtc">The next retry timestamp, or null to mark the message as failed permanently.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the status update has been staged.</returns>
        Task MarkFailedAsync(
            Guid id,
            string error,
            DateTimeOffset now,
            DateTimeOffset? nextAttemptOnUtc,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes published messages older than the specified timestamp.
        /// </summary>
        /// <param name="olderThanUtc">The maximum publish timestamp to remove.</param>
        /// <param name="batchSize">The maximum number of rows to delete.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>The number of messages staged for deletion.</returns>
        Task<int> CleanPublishedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits staged changes in the storage provider.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when changes are committed.</returns>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
