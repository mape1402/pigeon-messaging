namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Defines an interceptor that can inspect or modify a message
    /// before it is published to the message queue.
    /// </summary>
    public interface IPublishInterceptor
    {
        /// <summary>
        /// Intercepts the publishing process, allowing to add metadata,
        /// perform validations, logging, or other side effects before
        /// the message is sent.
        /// </summary>
        /// <param name="publishContext">The context of the publish operation,
        /// containing metadata and other information about the message.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for cooperative cancellation.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        ValueTask Intercept(PublishContext publishContext, CancellationToken cancellationToken = default);
    }
}
