namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// In-memory dispatch queue used to trigger outbox delivery immediately after a successful commit.
    /// </summary>
    public interface IOutboxDispatchQueue
    {
        ValueTask EnqueueAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);

        ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default);
    }
}
