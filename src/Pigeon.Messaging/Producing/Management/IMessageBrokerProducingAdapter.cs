namespace Pigeon.Messaging.Producing.Management
{
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Defines the contract for a producing adapter that publishes messages
    /// to a specific message broker implementation (e.g., RabbitMQ, Kafka, Azure Service Bus).
    /// </summary>
    public interface IMessageBrokerProducingAdapter
    {
        /// <summary>
        /// Publishes a wrapped message payload to the specified topic or queue.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message payload contained in the <see cref="WrappedPayload{T}"/>.
        /// </typeparam>
        /// <param name="payload">
        /// The strongly typed wrapped payload, including metadata such as domain, version, and creation timestamp.
        /// </param>
        /// <param name="topic">
        /// The target topic, queue, or routing key where the message should be published.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the publish operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous publish operation.
        /// </returns>
        ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Publishes a wrapped message payload to the specified publishing route.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message payload contained in the <see cref="WrappedPayload{T}"/>.
        /// </typeparam>
        /// <param name="payload">
        /// The strongly typed wrapped payload, including metadata such as domain, version, and creation timestamp.
        /// </param>
        /// <param name="route">
        /// The route used to publish the message.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the publish operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous publish operation.
        /// </returns>
        ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Publishes a raw message payload to the specified topic or queue without
        /// wrapping it in Pigeon metadata.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message payload.
        /// </typeparam>
        /// <param name="message">
        /// The strongly typed message that will be delivered directly to the broker.
        /// </param>
        /// <param name="topic">
        /// The target topic, queue, or routing key where the message should be published.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the publish operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous publish operation.
        /// </returns>
        ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Publishes a raw message payload to the specified publishing route without
        /// wrapping it in Pigeon metadata.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the message payload.
        /// </typeparam>
        /// <param name="message">
        /// The strongly typed message that will be delivered directly to the broker.
        /// </param>
        /// <param name="route">
        /// The route used to publish the message.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token that can be used to cancel the publish operation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> representing the asynchronous publish operation.
        /// </returns>
        ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default) where T : class;
    }
}
