namespace Pigeon.Messaging.Consuming.Configuration
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Represents the base configuration for a consumer,
    /// providing information about the topic, version, and
    /// the generic contract to handle incoming messages.
    /// </summary>
    public abstract class ConsumerConfiguration
    {
        /// <summary>
        /// Gets the topic or channel this consumer listens to.
        /// </summary>
        public string Topic { get; init; }

        /// <summary>
        /// Gets the semantic version of the message contract
        /// this consumer is compatible with.
        /// </summary>
        public SemanticVersion Version { get; init; }

        /// <summary>
        /// Gets the runtime type of the expected message payload.
        /// Useful for dynamic dispatch or validation.
        /// </summary>
        public abstract Type MessageType { get; }

        /// <summary>
        /// Gets the non-generic handler delegate used to
        /// process the message payload in a polymorphic way.
        /// </summary>
        public abstract ConsumeHandler Handler { get; }
    }

    /// <summary>
    /// Represents a typed consumer configuration for a specific
    /// message type <typeparamref name="T"/>.
    /// Encapsulates both the strongly typed handler and a generic
    /// fallback for runtime dispatch.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the message payload to be handled.
    /// </typeparam>
    public class ConsumerConfiguration<T> : ConsumerConfiguration where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerConfiguration{T}"/>
        /// class using a strongly typed consume handler.
        /// </summary>
        /// <param name="handler">
        /// The strongly typed handler delegate to process the incoming message.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the handler is null.
        /// </exception>
        public ConsumerConfiguration(ConsumeHandler<T> handler)
        {
            TypedHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            Handler = context => TypedHandler(context, (T)context.Message);
            MessageType = typeof(T);
        }

        /// <summary>
        /// Gets the strongly typed consume handler for the specific
        /// message type <typeparamref name="T"/>.
        /// </summary>
        public ConsumeHandler<T> TypedHandler { get; }

        /// <inheritdoc/>
        public override Type MessageType { get; }

        /// <inheritdoc/>
        public override ConsumeHandler Handler { get; }
    }
}
