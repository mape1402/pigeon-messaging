namespace Pigeon.Messaging.Producing
{
    using Pigeon.Messaging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;
    using System;

    /// <summary>
    /// Provides a base implementation for a message producer with support for publish interceptors,
    /// payload wrapping, and versioning.
    /// This abstract class defines the general workflow for publishing a message,
    /// delegating the actual publish operation to the producing manager.
    /// </summary>
    public class Producer : IProducer
    {
        private readonly IEnumerable<IPublishInterceptor> _interceptors;
        private readonly IProducingManager _producingManager;
        private readonly GlobalSettings _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="Producer"/> class.
        /// </summary>
        /// <param name="interceptors">
        /// A collection of publish interceptors that can enrich or modify the publish context
        /// before the message is sent to the message broker.
        /// </param>
        /// <param name="producingManager">
        /// The producing manager responsible for dispatching the final message payload
        /// to the configured message broker.
        /// </param>
        /// <param name="settings">
        /// The global messaging settings containing configuration details such as the domain name.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of the required dependencies are <c>null</c>.
        /// </exception>
        public Producer(IEnumerable<IPublishInterceptor> interceptors, IProducingManager producingManager, GlobalSettings settings)
        {
            _interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
            _producingManager = producingManager ?? throw new ArgumentNullException(nameof(producingManager));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public virtual async ValueTask PublishAsync<T>(T message, string topic, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            await PublishCore(message, topic, version, cancellationToken);
        }

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public virtual ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PublishAsync(message, topic, SemanticVersion.Default, cancellationToken);

        /// <summary>
        /// Defines the core logic for publishing a message.
        /// Applies all registered interceptors, wraps the payload with metadata and versioning,
        /// and delegates the final push to the producing manager.
        /// </summary>
        /// <typeparam name="T">The type of the message payload.</typeparam>
        /// <param name="message">The message instance to publish.</param>
        /// <param name="topic">The target topic or channel.</param>
        /// <param name="version">The semantic version of the message contract.</param>
        /// <param name="cancellationToken">
        /// A cancellation token to observe while waiting for the task to complete.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        protected virtual async ValueTask PublishCore<T>(T message, string topic, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            var publishContext = new PublishContext();

            foreach (var interceptor in _interceptors)
                await interceptor.Intercept(publishContext, cancellationToken);

            var payload = new WrappedPayload<T>
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Message = message,
                MessageVersion = version,
                Metadata = publishContext.GetMetadata(),
                Domain = _settings.Domain
            };

            await _producingManager.PushAsync(payload, topic, cancellationToken);
        }
    }
}
