namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Defines portable message settlement operations for broker deliveries.
    /// </summary>
    public interface IMessageSettlement
    {
        /// <summary>
        /// Acknowledges successful message processing.
        /// </summary>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous settlement operation.</returns>
        Task CompleteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests retry semantics for the message when the broker supports it.
        /// </summary>
        /// <param name="exception">The optional exception that caused the retry.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous settlement operation.</returns>
        Task RetryAsync(Exception exception = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Rejects the message when the broker supports it.
        /// </summary>
        /// <param name="exception">The optional exception that caused the rejection.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous settlement operation.</returns>
        Task RejectAsync(Exception exception = null, CancellationToken cancellationToken = default);
    }
}
