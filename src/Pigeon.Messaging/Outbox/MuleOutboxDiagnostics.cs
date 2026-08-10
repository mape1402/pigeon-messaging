namespace Pigeon.Messaging.Outbox
{
    using Mule.Diagnostics;

    /// <summary>
    /// Maps Mule diagnostics to the Pigeon outbox diagnostics contract.
    /// </summary>
    public sealed class MuleOutboxDiagnostics : IOutboxDiagnostics
    {
        private readonly IMuleDiagnostics _diagnostics;

        /// <summary>
        /// Initializes a new instance of the <see cref="MuleOutboxDiagnostics"/> class.
        /// </summary>
        /// <param name="diagnostics">The Mule diagnostics service.</param>
        public MuleOutboxDiagnostics(IMuleDiagnostics diagnostics)
        {
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        /// <inheritdoc />
        public async Task<OutboxDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = await _diagnostics.GetSnapshotAsync(cancellationToken);

            return new OutboxDiagnosticsSnapshot
            {
                PendingMessages = snapshot.Pending,
                LockedMessages = snapshot.Locked,
                PublishedMessages = snapshot.Completed,
                FailedMessages = snapshot.Failed,
                OldestPendingMessageOnUtc = snapshot.OldestPendingOnUtc,
                OldestFailedMessageOnUtc = snapshot.OldestFailedOnUtc
            };
        }
    }
}
