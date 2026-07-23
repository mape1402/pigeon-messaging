namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Defines the contract for a consuming dispatcher responsible for
    /// dispatching raw payloads to their corresponding message handlers
    /// based on topic and version.
    /// </summary>
    public interface IConsumingDispatcher
    {
        /// <summary>
        /// Dispatches a raw payload asynchronously by resolving the appropriate
        /// consumer configuration for the given topic and message version.
        /// </summary>
        /// <param name="topic">
        /// The topic or channel that the message was received from.
        /// </param>
        /// <param name="rawPayload">
        /// The raw payload containing minimal header information extracted
        /// from the original message, including domain, version, and timestamp.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe for cancellation requests.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous handling operation.
        /// </returns>
        Task DispatchAsync(string topic, RawPayload rawPayload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches a raw payload asynchronously by resolving the appropriate
        /// consumer configuration for the given topic, message version, and subscription.
        /// </summary>
        /// <param name="topic">
        /// The topic or channel that the message was received from.
        /// </param>
        /// <param name="subscription">
        /// The subscription, queue name, or consumer group that received the message.
        /// </param>
        /// <param name="rawPayload">
        /// The raw payload containing minimal header information extracted
        /// from the original message, including domain, version, and timestamp.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe for cancellation requests.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous handling operation.
        /// </returns>
        Task DispatchAsync(string topic, string subscription, RawPayload rawPayload, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatches a raw payload and attaches broker acknowledgement callbacks to the consume context.
        /// </summary>
        /// <param name="topic">The topic or channel that the message was received from.</param>
        /// <param name="subscription">The subscription, queue name, or consumer group that received the message.</param>
        /// <param name="rawPayload">The raw payload containing minimal header information.</param>
        /// <param name="completeAsync">Callback invoked to complete the message in the broker.</param>
        /// <param name="failAsync">Callback invoked to fail the message in the broker.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous handling operation.</returns>
        Task DispatchAsync(
            string topic,
            string subscription,
            RawPayload rawPayload,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync,
            CancellationToken cancellationToken = default);
    }
}
