namespace Pigeon.Messaging.Producing.Management
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Defines the contract for the core producing manager that controls
    /// the dispatching of messages to the underlying message broker adapters.
    /// </summary>
    public interface IProducingManager
    {
        /// <summary>
        /// Pushes the given wrapped payload to the specified topic.
        /// This operation delegates the actual delivery to the appropriate
        /// message broker producing adapter based on the configuration.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message payload.
        /// </typeparam>
        /// <param name="payload">
        /// The wrapped message payload containing metadata, version,
        /// and domain information.
        /// </param>
        /// <param name="topic">
        /// The topic or channel where the message will be published.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the asynchronous operation if needed.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> that completes when the message has been dispatched.
        /// </returns>
        ValueTask PushAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Pushes the given raw payload to the specified topic without wrapping it
        /// in Pigeon metadata.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message payload.
        /// </typeparam>
        /// <param name="message">
        /// The message payload that will be delivered directly to the broker.
        /// </param>
        /// <param name="topic">
        /// The topic or channel where the message will be published.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to cancel the asynchronous operation if needed.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> that completes when the message has been dispatched.
        /// </returns>
        ValueTask PushRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class;
    }
}
