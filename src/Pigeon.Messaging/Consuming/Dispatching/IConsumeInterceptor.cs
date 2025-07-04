namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Defines a contract for interceptors that run before a message is dispatched
    /// to its consumer handler.
    /// </summary>
    /// <remarks>
    /// Implementations can inspect or enrich the <see cref="ConsumeContext"/>,
    /// add custom logic such as logging, tracing, validation, or tenant resolution,
    /// and even short-circuit the pipeline if needed.
    /// </remarks>
    public interface IConsumeInterceptor
    {
        /// <summary>
        /// Invoked for each consumed message before it reaches the final handler.
        /// </summary>
        /// <param name="context">
        /// The <see cref="ConsumeContext"/> containing the message,
        /// metadata, topic, version, and scoped services.
        /// </param>
        /// <param name="cancellationToken">
        /// A token for cooperative cancellation.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask"/> that represents the asynchronous interception operation.
        /// </returns>
        ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default);
    }
}
