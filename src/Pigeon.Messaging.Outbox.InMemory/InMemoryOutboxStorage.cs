namespace Pigeon.Messaging.Outbox.InMemory
{
    using Pigeon.Messaging.Outbox;
    using System.Transactions;

    internal sealed class InMemoryOutboxStorage : IOutboxStorage
    {
        private readonly InMemoryOutboxStore _store;
        private readonly List<OutboxMessage> _pendingAdds = new();

        public InMemoryOutboxStorage(InMemoryOutboxStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            _pendingAdds.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<OutboxMessage>> LockPendingAsync(
            int batchSize,
            TimeSpan lockTimeout,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_store.GetPending(batchSize, lockTimeout, now));

        public Task<OutboxMessage> LockAsync(Guid id, TimeSpan lockTimeout, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.Lock(id, lockTimeout, now));

        public Task MarkPublishedAsync(Guid id, DateTimeOffset publishedOnUtc, CancellationToken cancellationToken = default)
        {
            _store.MarkPublished(id, publishedOnUtc);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid id,
            string error,
            DateTimeOffset now,
            DateTimeOffset? nextAttemptOnUtc,
            CancellationToken cancellationToken = default)
        {
            _store.MarkFailed(id, error, nextAttemptOnUtc);
            return Task.CompletedTask;
        }

        public Task<int> CleanPublishedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.CleanPublished(olderThanUtc, batchSize));

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_pendingAdds.Count == 0)
                return Task.CompletedTask;

            var messages = _pendingAdds.Select(InMemoryOutboxStore.Clone).ToArray();

            _pendingAdds.Clear();

            var transaction = Transaction.Current;
            if (transaction == null)
            {
                Commit(messages);
                return Task.CompletedTask;
            }

            transaction.TransactionCompleted += (_, args) =>
            {
                if (args.Transaction.TransactionInformation.Status == TransactionStatus.Committed)
                    Commit(messages);
            };

            return Task.CompletedTask;
        }

        private void Commit(IEnumerable<OutboxMessage> messages)
        {
            foreach (var message in messages)
                _store.Add(message);
        }
    }
}
