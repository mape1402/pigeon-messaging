namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Allows a storage provider to prepare its schema when auto-create mode is enabled.
    /// </summary>
    public interface IOutboxSchemaInitializer
    {
        /// <summary>
        /// Ensures the provider-specific outbox schema exists.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when schema initialization has finished.</returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
