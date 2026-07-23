namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Allows a storage provider to prepare its schema when auto-create mode is enabled.
    /// </summary>
    public interface IOutboxSchemaInitializer
    {
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
