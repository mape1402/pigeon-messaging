namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Provides read-only diagnostics for the configured outbox storage.
    /// </summary>
    public interface IOutboxDiagnostics
    {
        /// <summary>
        /// Gets a point-in-time snapshot of outbox message state.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>The current outbox diagnostics snapshot.</returns>
        Task<OutboxDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    }
}
