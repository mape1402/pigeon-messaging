namespace Pigeon.Messaging.Outbox
{
    public interface IOutboxDiagnostics
    {
        Task<OutboxDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    }
}
