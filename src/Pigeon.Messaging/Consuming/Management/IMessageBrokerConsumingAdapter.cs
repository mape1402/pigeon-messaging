namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Defines the contract for an adapter that connects to the underlying message broker
    /// and raises an event when messages are consumed.
    /// </summary>
    public interface IMessageBrokerConsumingAdapter
    {
        /// <summary>
        /// Starts listening for incoming messages.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the task to complete.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous start operation.</returns>
        ValueTask StartConsumeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops listening for messages and closes any open connections.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to observe while waiting for the task to complete.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> that represents the asynchronous stop operation.</returns>
        ValueTask StopConsumeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Raised when a new message is consumed from the broker.
        /// </summary>
        event EventHandler<MessageConsumedEventArgs> MessageConsumed;
    }
}
