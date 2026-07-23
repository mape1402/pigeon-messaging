namespace Pigeon.Messaging.Outbox.InMemory
{
    using Pigeon.Messaging.Outbox;

    /// <summary>
    /// Exposes the process-local in-memory outbox state for tests and samples.
    /// </summary>
    public interface IInMemoryOutbox
    {
        /// <summary>
        /// Gets a point-in-time copy of all messages currently tracked by the in-memory outbox.
        /// </summary>
        IReadOnlyCollection<OutboxMessage> Messages { get; }

        /// <summary>
        /// Clears all tracked outbox messages.
        /// </summary>
        void Clear();
    }
}
