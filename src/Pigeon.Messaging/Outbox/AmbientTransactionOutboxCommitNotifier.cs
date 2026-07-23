namespace Pigeon.Messaging.Outbox
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using System.Transactions;

    internal sealed class AmbientTransactionOutboxCommitNotifier : IOutboxCommitNotifier
    {
        private readonly IOutboxDispatchQueue _dispatchQueue;
        private readonly GlobalSettings _settings;
        private readonly ILogger<AmbientTransactionOutboxCommitNotifier> _logger;

        public AmbientTransactionOutboxCommitNotifier(
            IOutboxDispatchQueue dispatchQueue,
            IOptions<GlobalSettings> settings,
            ILogger<AmbientTransactionOutboxCommitNotifier> logger = null)
        {
            _dispatchQueue = dispatchQueue ?? throw new ArgumentNullException(nameof(dispatchQueue));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<AmbientTransactionOutboxCommitNotifier>.Instance;
        }

        public async ValueTask NotifySavedAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
        {
            if (_settings.Outbox?.Enabled != true || _settings.Outbox.ImmediateDispatch != true)
                return;

            var transaction = Transaction.Current;

            if (transaction == null)
            {
                await _dispatchQueue.EnqueueAsync(outboxMessageId, cancellationToken);
                return;
            }

            transaction.TransactionCompleted += (_, args) =>
            {
                if (args.Transaction.TransactionInformation.Status != TransactionStatus.Committed)
                    return;

                ThreadPool.QueueUserWorkItem(_ => _ = EnqueueCommittedMessageAsync(outboxMessageId));
            };
        }

        private async Task EnqueueCommittedMessageAsync(Guid outboxMessageId)
        {
            try
            {
                await _dispatchQueue.EnqueueAsync(outboxMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox message {OutboxMessageId} could not be queued after transaction commit.", outboxMessageId);
            }
        }
    }
}
