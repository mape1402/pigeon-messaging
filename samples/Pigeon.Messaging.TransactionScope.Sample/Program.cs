using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;
using System.Collections.Concurrent;
using System.Transactions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    ["Pigeon:Domain"] = "transaction-scope-sample"
});

builder.Services.AddSingleton<PublishedMessages>();
builder.Services.AddSingleton<TransactionalInMemoryOutboxStore>();

builder.Services.AddPigeon(builder.Configuration, pigeon =>
{
    pigeon.ConfigureOutbox(outbox =>
    {
        outbox.Enabled = true;
        outbox.ImmediateDispatch = true;
        outbox.DispatchInterval = TimeSpan.FromMinutes(10);
        outbox.CleanInterval = TimeSpan.FromMinutes(10);
        outbox.SchemaMode = OutboxSchemaMode.Migrations;
    });

    pigeon.AddFeature(feature =>
    {
        feature.Services.AddScoped<IOutboxStorage>(provider =>
            provider.GetRequiredService<TransactionalInMemoryOutboxStore>().CreateStorage());
        feature.Services.AddSingleton<IMessageBrokerProducingAdapter, CapturingProducingAdapter>();
    });
});

using var host = builder.Build();
await host.StartAsync();

var publishedMessages = host.Services.GetRequiredService<PublishedMessages>();

await PublishInsideCommittedTransactionAsync(host.Services);
await publishedMessages.WaitForCountAsync(1, TimeSpan.FromSeconds(5));

await PublishInsideRolledBackTransactionAsync(host.Services);
await Task.Delay(TimeSpan.FromSeconds(1));

if (publishedMessages.Count != 1)
    throw new InvalidOperationException($"Expected only one committed message, but {publishedMessages.Count} messages were published.");

var published = publishedMessages.Snapshot.Single();
Console.WriteLine($"Published message: {published.MessageType} via topic '{published.Route.Topic}'.");
Console.WriteLine("Rollback message was not dispatched.");

await host.StopAsync();

static async Task PublishInsideCommittedTransactionAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

    var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
    await producer.PublishAsync(new SampleMessage("committed"), "sample.transaction-scope");

    transaction.Complete();
}

static async Task PublishInsideRolledBackTransactionAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

    var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
    await producer.PublishAsync(new SampleMessage("rolled-back"), "sample.transaction-scope");
}

internal sealed record SampleMessage(string Text);

internal sealed record PublishedEnvelope(string Kind, string MessageType, PublishingRoute Route);

internal sealed class PublishedMessages
{
    private readonly ConcurrentQueue<PublishedEnvelope> _messages = new();

    public int Count => _messages.Count;

    public IReadOnlyCollection<PublishedEnvelope> Snapshot => _messages.ToArray();

    public void Add(PublishedEnvelope message)
        => _messages.Enqueue(message);

    public async Task WaitForCountAsync(int expectedCount, TimeSpan timeout)
    {
        var expiresOn = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < expiresOn)
        {
            if (Count >= expectedCount)
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException($"Expected {expectedCount} published message(s), but found {Count}.");
    }
}

internal sealed class CapturingProducingAdapter : IMessageBrokerProducingAdapter
{
    private readonly PublishedMessages _publishedMessages;

    public CapturingProducingAdapter(PublishedMessages publishedMessages)
    {
        _publishedMessages = publishedMessages;
    }

    public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
        where T : class
        => PublishMessageAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

    public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
        where T : class
    {
        _publishedMessages.Add(new PublishedEnvelope("wrapped", typeof(T).Name, route));
        return ValueTask.CompletedTask;
    }

    public ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
        where T : class
        => PublishRawMessageAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

    public ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
        where T : class
    {
        _publishedMessages.Add(new PublishedEnvelope("raw", typeof(T).Name, route));
        return ValueTask.CompletedTask;
    }
}

internal sealed class TransactionalInMemoryOutboxStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, OutboxMessage> _messages = new();

    public IOutboxStorage CreateStorage()
        => new TransactionalInMemoryOutboxStorage(this);

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

internal sealed class TransactionalInMemoryOutboxStorage : IOutboxStorage
{
    private readonly TransactionalInMemoryOutboxStore _store;
    private readonly List<OutboxMessage> _pendingAdds = new();

    public TransactionalInMemoryOutboxStorage(TransactionalInMemoryOutboxStore store)
    {
        _store = store;
    }

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
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

        var messages = _pendingAdds.ToArray();
        _pendingAdds.Clear();

        var transaction = Transaction.Current;
        if (transaction == null)
        {
            _store.Commit(messages);
            return Task.CompletedTask;
        }

        transaction.TransactionCompleted += (_, args) =>
        {
            if (args.Transaction.TransactionInformation.Status == TransactionStatus.Committed)
                _store.Commit(messages);
        };

        return Task.CompletedTask;
    }
}
