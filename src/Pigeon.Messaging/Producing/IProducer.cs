namespace Pigeon.Messaging.Producing
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Defines an asynchronous producer for sending messages to topics,
    /// optionally including a semantic version for the message contract.
    /// </summary>
    public interface IProducer
    {
        /// <summary>
        /// Publishes a message asynchronously to a given topic with an explicit semantic version.
        /// </summary>
        /// <typeparam name="T">The type of the message payload.</typeparam>
        /// <param name="message">The message instance to publish.</param>
        /// <param name="topic">The target topic or channel.</param>
        /// <param name="version">The semantic version of the message contract.</param>
        /// <param name="cancellationToken">
        /// A token to monitor for cancellation requests.
        /// This allows the operation to be cancelled before completion.
        /// </param>
        /// <returns>A ValueTask representing the asynchronous operation.</returns>
        ValueTask PublishAsync<T>(T message, string topic, SemanticVersion version, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Publishes a message asynchronously to a given topic without specifying a version.
        /// The publisher may apply a default version or no versioning at all.
        /// </summary>
        /// <typeparam name="T">The type of the message payload.</typeparam>
        /// <param name="message">The message instance to publish.</param>
        /// <param name="topic">The target topic or channel.</param>
        /// <param name="cancellationToken">
        /// A token to monitor for cancellation requests.
        /// This allows the operation to be cancelled before completion.
        /// </param>
        /// <returns>A ValueTask representing the asynchronous operation.</returns>
        ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class;
    }
}
