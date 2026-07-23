using Pigeon.Messaging.Outbox;

internal sealed class TransactionalInMemoryOutboxStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, OutboxMessage> _messages = new();

    public IOutboxStorage CreateStorage()
        => new TransactionalInMemoryOutboxStorage(this);

    public IReadOnlyCollection<OutboxMessage> Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _messages.Values
                    .OrderBy(message => message.CreatedOnUtc)
                    .Select(Clone)
                    .ToArray();
            }
        }
    }

    public void Commit(IReadOnlyCollection<OutboxMessage> messages)
    {
        lock (_gate)
        {
            foreach (var message in messages)
                _messages[message.Id] = Clone(message);
        }
    }

    public IReadOnlyCollection<OutboxMessage> GetPending(int batchSize, TimeSpan lockTimeout, DateTimeOffset now)
    {
        lock (_gate)
        {
            var lockExpiration = now.Subtract(lockTimeout);

            return _messages.Values
                .Where(message =>
                    message.Status == OutboxMessageStatus.Pending && (message.NextAttemptOnUtc == null || message.NextAttemptOnUtc <= now) ||
                    message.Status == OutboxMessageStatus.Locked && message.LockedOnUtc <= lockExpiration)
                .OrderBy(message => message.CreatedOnUtc)
                .Take(batchSize)
                .Select(Clone)
                .ToArray();
        }
    }

    public OutboxMessage Lock(Guid id, TimeSpan lockTimeout, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(id, out var message))
                return null;

            var lockExpiration = now.Subtract(lockTimeout);
            var canLock =
                message.Status == OutboxMessageStatus.Pending && (message.NextAttemptOnUtc == null || message.NextAttemptOnUtc <= now) ||
                message.Status == OutboxMessageStatus.Locked && message.LockedOnUtc <= lockExpiration;

            if (!canLock)
                return null;

            message.Status = OutboxMessageStatus.Locked;
            message.LockedOnUtc = now;

            return Clone(message);
        }
    }

    public void MarkPublished(Guid id, DateTimeOffset publishedOnUtc)
    {
        lock (_gate)
        {
            var message = Find(id);
            message.Status = OutboxMessageStatus.Published;
            message.PublishedOnUtc = publishedOnUtc;
            message.LockedOnUtc = null;
            message.NextAttemptOnUtc = null;
            message.LastError = null;
        }
    }

    public void MarkFailed(Guid id, string error, DateTimeOffset? nextAttemptOnUtc)
    {
        lock (_gate)
        {
            var message = Find(id);
            message.Attempts++;
            message.Status = nextAttemptOnUtc == null ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
            message.LastError = error;
            message.LockedOnUtc = null;
            message.NextAttemptOnUtc = nextAttemptOnUtc;
        }
    }

    public int CleanPublished(DateTimeOffset olderThanUtc, int batchSize)
    {
        lock (_gate)
        {
            var ids = _messages.Values
                .Where(message => message.Status == OutboxMessageStatus.Published && message.PublishedOnUtc <= olderThanUtc)
                .OrderBy(message => message.PublishedOnUtc)
                .Take(batchSize)
                .Select(message => message.Id)
                .ToArray();

            foreach (var id in ids)
                _messages.Remove(id);

            return ids.Length;
        }
    }

    private OutboxMessage Find(Guid id)
        => _messages.TryGetValue(id, out var message)
            ? message
            : throw new InvalidOperationException($"Outbox message '{id}' was not found.");

    private static OutboxMessage Clone(OutboxMessage message)
        => new()
        {
            Id = message.Id,
            Payload = message.Payload,
            PayloadType = message.PayloadType,
            IsRaw = message.IsRaw,
            Topic = message.Topic,
            Exchange = message.Exchange,
            RoutingKey = message.RoutingKey,
            Status = message.Status,
            Attempts = message.Attempts,
            LastError = message.LastError,
            CreatedOnUtc = message.CreatedOnUtc,
            LockedOnUtc = message.LockedOnUtc,
            NextAttemptOnUtc = message.NextAttemptOnUtc,
            PublishedOnUtc = message.PublishedOnUtc
        };
}
