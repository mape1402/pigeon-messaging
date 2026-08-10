namespace Pigeon.Messaging.Outbox.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore;
    using Mule;
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
            var actions = await dbContext.Set<DurableAction>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            actions = actions
                .Where(x => x.Key.Equals(PigeonOutboxActionKeys.Publish))
                .ToList();
            var failedActions = actions
                .Where(x => x.Status == DurableActionStatus.Failed)
                .OrderByDescending(x => x.CreatedOnUtc)
                .ToList();

            return new OutboxDiagnosticsSnapshot
            {
                PendingMessages = actions.Count(x => x.Status == DurableActionStatus.Pending),
                LockedMessages = actions.Count(x => x.Status == DurableActionStatus.Locked),
                PublishedMessages = actions.Count(x => x.Status == DurableActionStatus.Completed),
                FailedMessages = failedActions.Count,
                OldestPendingMessageOnUtc = actions
                    .Where(x => x.Status == DurableActionStatus.Pending)
                    .OrderBy(x => x.CreatedOnUtc)
                    .Select(x => (DateTimeOffset?)x.CreatedOnUtc)
                    .FirstOrDefault(),
                OldestFailedMessageOnUtc = failedActions
                    .OrderBy(x => x.CreatedOnUtc)
                    .Select(x => (DateTimeOffset?)x.CreatedOnUtc)
                    .FirstOrDefault(),
                LastFailure = failedActions.Select(x => x.LastError).FirstOrDefault()
            };
        }
    }
}
