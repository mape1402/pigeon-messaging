namespace Pigeon.Messaging.Consuming
{
    /// <summary>
    /// Represents a delegate that handles a consumed message of a specific type,
    /// providing access to the <see cref="ConsumeContext"/> and the deserialized payload.
    /// </summary>
    /// <typeparam name="T">
    /// The expected type of the message payload.
    /// </typeparam>
    /// <param name="consumeContext">
    /// The context containing metadata, topic, version, and scoped services.
    /// </param>
    /// <param name="message">
    /// The strongly-typed deserialized message payload.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous handling operation.
    /// </returns>
    public delegate Task ConsumeHandler<T>(ConsumeContext consumeContext, T message) where T : class;

    /// <summary>
    /// Represents a delegate that handles a consumed message using only the <see cref="ConsumeContext"/>,
    /// where the message payload is accessed via <see cref="ConsumeContext.Message"/>.
    /// </summary>
    /// <param name="consumeContext">
    /// The context containing metadata, topic, version, the raw message payload, and scoped services.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous handling operation.
    /// </returns>
    public delegate Task ConsumeHandler(ConsumeContext consumeContext);
}
