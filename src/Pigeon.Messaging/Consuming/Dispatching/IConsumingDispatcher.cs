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
    }
}
