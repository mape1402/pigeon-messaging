namespace Pigeon.Messaging.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore;
    using Pigeon.Messaging.Outbox;

    internal sealed class EntityFrameworkOutboxDiagnostics<TDbContext> : IOutboxDiagnostics
        where TDbContext : DbContext
    {
        private readonly IOutboxDbContextFactory<TDbContext> _dbContextFactory;

        public EntityFrameworkOutboxDiagnostics(IOutboxDbContextFactory<TDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        }

        public async Task<OutboxDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            await using var dbContext = _dbContextFactory.CreateDbContext();
            var messages = await dbContext.Set<OutboxMessage>().AsNoTracking().ToListAsync(cancellationToken);
            var failedMessages = messages
                .Where(x => x.Status == OutboxMessageStatus.Failed)
                .OrderByDescending(x => x.CreatedOnUtc)
                .ToList();

            return new OutboxDiagnosticsSnapshot
            {
                PendingMessages = messages.Count(x => x.Status == OutboxMessageStatus.Pending),
                LockedMessages = messages.Count(x => x.Status == OutboxMessageStatus.Locked),
                PublishedMessages = messages.Count(x => x.Status == OutboxMessageStatus.Published),
                FailedMessages = failedMessages.Count,
                OldestPendingMessageOnUtc = messages
                    .Where(x => x.Status == OutboxMessageStatus.Pending)
                    .OrderBy(x => x.CreatedOnUtc)
                    .Select(x => (DateTimeOffset?)x.CreatedOnUtc)
                    .FirstOrDefault(),
                OldestFailedMessageOnUtc = failedMessages
                    .OrderBy(x => x.CreatedOnUtc)
                    .Select(x => (DateTimeOffset?)x.CreatedOnUtc)
                    .FirstOrDefault(),
                LastFailure = failedMessages.Select(x => x.LastError).FirstOrDefault()
            };
        }
    }
}
