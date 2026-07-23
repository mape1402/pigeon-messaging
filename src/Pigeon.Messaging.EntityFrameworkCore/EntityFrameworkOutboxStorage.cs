namespace Pigeon.Messaging.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore;
    using Pigeon.Messaging.Outbox;

    internal sealed class EntityFrameworkOutboxStorage<TDbContext> : IOutboxStorage
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        public EntityFrameworkOutboxStorage(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
            => await _dbContext.Set<OutboxMessage>().AddAsync(message, cancellationToken);

        public async Task<IReadOnlyCollection<OutboxMessage>> LockPendingAsync(
            int batchSize,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var lockExpiration = now.Subtract(lockTimeout);
            var candidates = await _dbContext.Set<OutboxMessage>()
                .Where(x => x.Status == OutboxMessageStatus.Pending || x.Status == OutboxMessageStatus.Locked)
                .ToListAsync(cancellationToken);

            var messages = candidates
                .Where(x =>
                    x.Status == OutboxMessageStatus.Pending && (x.NextAttemptOnUtc == null || x.NextAttemptOnUtc <= now) ||
                    x.Status == OutboxMessageStatus.Locked && x.LockedOnUtc <= lockExpiration)
                .OrderBy(x => x.CreatedOnUtc)
                .Take(batchSize)
                .ToList();

            return messages;
        }

        public async Task<OutboxMessage> LockAsync(
            Guid id,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var lockExpiration = now.Subtract(lockTimeout);
            var message = await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { id }, cancellationToken);

            if (message == null)
                return null;

            var canLock =
                message.Status == OutboxMessageStatus.Pending && (message.NextAttemptOnUtc == null || message.NextAttemptOnUtc <= now) ||
                message.Status == OutboxMessageStatus.Locked && message.LockedOnUtc <= lockExpiration;

            if (!canLock)
                return null;

            message.Status = OutboxMessageStatus.Locked;
            message.LockedOnUtc = now;

            return message;
        }

        public async Task MarkPublishedAsync(Guid id, DateTimeOffset publishedOnUtc, CancellationToken cancellationToken = default)
        {
            var message = await FindAsync(id, cancellationToken);
            message.Status = OutboxMessageStatus.Published;
            message.PublishedOnUtc = publishedOnUtc;
            message.LockedOnUtc = null;
            message.NextAttemptOnUtc = null;
            message.LastError = null;
        }

        public async Task MarkFailedAsync(
            Guid id,
            string error,
            DateTimeOffset now,
            DateTimeOffset? nextAttemptOnUtc,
            CancellationToken cancellationToken = default)
        {
            var message = await FindAsync(id, cancellationToken);
            message.Attempts++;
            message.Status = nextAttemptOnUtc == null ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
            message.LastError = error;
            message.LockedOnUtc = null;
            message.NextAttemptOnUtc = nextAttemptOnUtc;
        }

        public async Task<int> CleanPublishedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken = default)
        {
            var candidates = await _dbContext.Set<OutboxMessage>()
                .Where(x => x.Status == OutboxMessageStatus.Published)
                .ToListAsync(cancellationToken);

            var messages = candidates
                .Where(x => x.PublishedOnUtc <= olderThanUtc)
                .OrderBy(x => x.PublishedOnUtc)
                .Take(batchSize)
                .ToList();

            _dbContext.Set<OutboxMessage>().RemoveRange(messages);
            return messages.Count;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => _dbContext.SaveChangesAsync(cancellationToken);

        private async Task<OutboxMessage> FindAsync(Guid id, CancellationToken cancellationToken)
            => await _dbContext.Set<OutboxMessage>().FindAsync(new object[] { id }, cancellationToken)
                ?? throw new InvalidOperationException($"Outbox message '{id}' was not found.");
    }
}
