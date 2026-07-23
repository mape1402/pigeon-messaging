namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Defines the persistence contract used by the Pigeon transactional outbox.
    /// </summary>
    public interface IOutboxStorage
    {
        Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<OutboxMessage>> LockPendingAsync(
            int batchSize,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        Task<OutboxMessage> LockAsync(
            Guid id,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);

        Task MarkPublishedAsync(Guid id, DateTimeOffset publishedOnUtc, CancellationToken cancellationToken = default);

        Task MarkFailedAsync(
            Guid id,
            string error,
            DateTimeOffset now,
            DateTimeOffset? nextAttemptOnUtc,
            CancellationToken cancellationToken = default);

        Task<int> CleanPublishedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
