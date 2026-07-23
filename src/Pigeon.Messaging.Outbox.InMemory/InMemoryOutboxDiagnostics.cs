namespace Pigeon.Messaging.Outbox.InMemory
{
    using Pigeon.Messaging.Outbox;

    internal sealed class InMemoryOutboxDiagnostics : IOutboxDiagnostics
    {
        private readonly IInMemoryOutbox _outbox;

        public InMemoryOutboxDiagnostics(IInMemoryOutbox outbox)
        {
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        }

        public Task<OutboxDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var messages = _outbox.Messages;
            var failedMessages = messages
                .Where(message => message.Status == OutboxMessageStatus.Failed)
                .OrderByDescending(message => message.CreatedOnUtc)
                .ToArray();

            return Task.FromResult(new OutboxDiagnosticsSnapshot
            {
                PendingMessages = messages.Count(message => message.Status == OutboxMessageStatus.Pending),
                LockedMessages = messages.Count(message => message.Status == OutboxMessageStatus.Locked),
                PublishedMessages = messages.Count(message => message.Status == OutboxMessageStatus.Published),
                FailedMessages = failedMessages.Length,
                OldestPendingMessageOnUtc = messages
                    .Where(message => message.Status == OutboxMessageStatus.Pending)
                    .OrderBy(message => message.CreatedOnUtc)
                    .Select(message => (DateTimeOffset?)message.CreatedOnUtc)
                    .FirstOrDefault(),
                OldestFailedMessageOnUtc = failedMessages
                    .OrderBy(message => message.CreatedOnUtc)
                    .Select(message => (DateTimeOffset?)message.CreatedOnUtc)
                    .FirstOrDefault(),
                LastFailure = failedMessages.Select(message => message.LastError).FirstOrDefault()
            });
        }
    }
}
