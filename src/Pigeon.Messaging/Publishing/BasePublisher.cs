namespace Pigeon.Messaging.Publishing
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Provides a base implementation for a message publisher with support for publish interceptors,
    /// payload wrapping, and versioning.
    /// This abstract class defines the general workflow for publishing a message,
    /// delegating the actual publish operation to the specific message broker implementation.
    /// </summary>
    public abstract class BasePublisher : IPublisher
    {
        private readonly IEnumerable<IPublishInterceptor> _interceptors;
        private readonly MessagingOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="BasePublisher"/> class.
        /// </summary>
        /// <param name="interceptors">
        /// A collection of publish interceptors that can enrich or modify the publish context
        /// before the message is sent to the message broker.
        /// </param>
        /// <param name="options">
        /// The messaging options containing configuration details such as the domain name.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="interceptors"/> or <paramref name="options"/> is null.
        /// </exception>
        protected BasePublisher(IEnumerable<IPublishInterceptor> interceptors, IOptions<MessagingOptions> options)
        {
            _interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public async ValueTask PublishAsync<T>(T message, string topic, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            var publishContext = new PublishContext();

            foreach (var interceptor in _interceptors)
                await interceptor.Intercept(publishContext);

            var payload = new WrappedPayload<T>
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Message = message,
                MessageVersion = version,
                Metadata = publishContext.GetMetadata(),
                Domain = _options.Domain
            };

            await PublishCore(payload, topic, cancellationToken);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PublishAsync(message, topic, SemanticVersion.Default, cancellationToken);

        /// <summary>
        /// Publishes the wrapped payload to the underlying message broker.
        /// Implement this method to handle the specific publish logic for
        /// RabbitMQ, Kafka, Azure Service Bus, etc.
        /// </summary>
        /// <typeparam name="T">The type of the message payload.</typeparam>
        /// <param name="payload">The wrapped payload including metadata, domain, version, and message.</param>
        /// <param name="topic">The target topic or queue.</param>
        /// <param name="cancellationToken">
        /// A token to monitor for cancellation requests.
        /// This allows the operation to be cancelled before completion.
        /// </param>
        /// <returns>A ValueTask representing the asynchronous operation.</returns>
        protected abstract ValueTask PublishCore<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class;
    }
}
