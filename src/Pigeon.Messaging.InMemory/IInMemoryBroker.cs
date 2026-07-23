namespace Pigeon.Messaging.InMemory
{
    /// <summary>
    /// Exposes the state captured by the in-memory broker for tests and diagnostics.
    /// </summary>
    public interface IInMemoryBroker
    {
        /// <summary>
        /// Gets all messages published through this in-memory broker instance.
        /// </summary>
        IReadOnlyCollection<InMemoryPublishedMessage> PublishedMessages { get; }

        /// <summary>
        /// Gets all deliveries created for matching consumer subscriptions.
        /// </summary>
        IReadOnlyCollection<InMemoryDelivery> Deliveries { get; }

        /// <summary>
        /// Clears published messages and deliveries.
        /// </summary>
        void Clear();
    }
}
