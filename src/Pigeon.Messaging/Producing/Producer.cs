namespace Pigeon.Messaging.Producing
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing.Management;
    using System;

    /// <summary>
    /// Provides a base implementation for a message producer with support for publish interceptors,
    /// payload wrapping, versioning, and optional transactional outbox persistence.
    /// </summary>
    public class Producer : IProducer
    {
        private readonly IEnumerable<IPublishInterceptor> _interceptors;
        private readonly IProducingManager _producingManager;
        private readonly GlobalSettings _settings;
        private readonly IOutboxStorage _outboxStorage;
        private readonly OutboxMessageFactory _outboxMessageFactory;
        private readonly IOutboxCommitNotifier _outboxCommitNotifier;

        /// <summary>
        /// Initializes a new producer without outbox persistence.
        /// </summary>
        /// <param name="interceptors">The publish interceptors executed before wrapped publishing.</param>
        /// <param name="producingManager">The producing manager that dispatches messages to broker adapters.</param>
        /// <param name="settings">The global Pigeon settings.</param>
        public Producer(IEnumerable<IPublishInterceptor> interceptors, IProducingManager producingManager, IOptions<GlobalSettings> settings)
            : this(interceptors, producingManager, settings, null, null)
        {
        }

        /// <summary>
        /// Initializes a new producer with outbox persistence.
        /// </summary>
        /// <param name="interceptors">The publish interceptors executed before wrapped publishing.</param>
        /// <param name="producingManager">The producing manager that dispatches messages to broker adapters.</param>
        /// <param name="settings">The global Pigeon settings.</param>
        /// <param name="outboxStorage">The optional outbox storage used when outbox is enabled.</param>
        /// <param name="outboxMessageFactory">The optional factory that creates persisted outbox messages.</param>
        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IOutboxStorage outboxStorage,
            OutboxMessageFactory outboxMessageFactory)
            : this(interceptors, producingManager, settings, outboxStorage, outboxMessageFactory, null)
        {
        }

        /// <summary>
        /// Initializes a new producer with outbox persistence and commit-aware dispatch notification.
        /// </summary>
        /// <param name="interceptors">The publish interceptors executed before wrapped publishing.</param>
        /// <param name="producingManager">The producing manager that dispatches messages to broker adapters.</param>
        /// <param name="settings">The global Pigeon settings.</param>
        /// <param name="outboxStorage">The optional outbox storage used when outbox is enabled.</param>
        /// <param name="outboxMessageFactory">The optional factory that creates persisted outbox messages.</param>
        /// <param name="outboxCommitNotifier">The optional notifier that queues messages after save or transaction commit.</param>
        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IOutboxStorage outboxStorage,
            OutboxMessageFactory outboxMessageFactory,
            IOutboxCommitNotifier outboxCommitNotifier)
        {
            _interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
            _producingManager = producingManager ?? throw new ArgumentNullException(nameof(producingManager));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _outboxStorage = outboxStorage;
            _outboxMessageFactory = outboxMessageFactory;
            _outboxCommitNotifier = outboxCommitNotifier;
        }

        /// <summary>
        /// Publishes a message to a topic using the specified semantic version.
        /// </summary>
        /// <typeparam name="T">The message payload type.</typeparam>
        /// <param name="message">The message to publish.</param>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="version">The semantic version for the message contract.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been published or persisted to outbox.</returns>
        public virtual async ValueTask PublishAsync<T>(T message, string topic, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            await PublishCore(message, PublishingRoute.ForTopic(topic), version, cancellationToken);
        }

        /// <summary>
        /// Publishes a message to a broker exchange and routing key using the specified semantic version.
        /// </summary>
        /// <typeparam name="T">The message payload type.</typeparam>
        /// <param name="message">The message to publish.</param>
        /// <param name="exchange">The broker exchange.</param>
        /// <param name="routingKey">The broker routing key.</param>
        /// <param name="version">The semantic version for the message contract.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been published or persisted to outbox.</returns>
        public virtual async ValueTask PublishAsync<T>(T message, string exchange, string routingKey, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            await PublishCore(message, PublishingRoute.ForExchange(exchange, routingKey), version, cancellationToken);
        }

        /// <summary>
        /// Publishes a message to a topic using the default semantic version.
        /// </summary>
        /// <typeparam name="T">The message payload type.</typeparam>
        /// <param name="message">The message to publish.</param>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been published or persisted to outbox.</returns>
        public virtual ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PublishAsync(message, topic, SemanticVersion.Default, cancellationToken);

        /// <summary>
        /// Publishes a raw message to a topic without the Pigeon wrapper.
        /// </summary>
        /// <typeparam name="T">The message payload type.</typeparam>
        /// <param name="message">The raw message to publish.</param>
        /// <param name="topic">The topic to publish to.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been published or persisted to outbox.</returns>
        public virtual async ValueTask PublishRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            await PublishRawCore(message, PublishingRoute.ForTopic(topic), cancellationToken);
        }

        /// <summary>
        /// Publishes a raw message to a broker exchange and routing key without the Pigeon wrapper.
        /// </summary>
        /// <typeparam name="T">The message payload type.</typeparam>
        /// <param name="message">The raw message to publish.</param>
        /// <param name="exchange">The broker exchange.</param>
        /// <param name="routingKey">The broker routing key.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been published or persisted to outbox.</returns>
        public virtual async ValueTask PublishRawAsync<T>(T message, string exchange, string routingKey, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            await PublishRawCore(message, PublishingRoute.ForExchange(exchange, routingKey), cancellationToken);
        }

        /// <summary>
        /// Executes the wrapped publish pipeline for a message and route.
        /// </summary>
        /// <typeparam name="T">The message payload type.</typeparam>
        /// <param name="message">The message to publish.</param>
        /// <param name="route">The publishing route.</param>
        /// <param name="version">The semantic version for the message contract.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task that completes when the message has been published or persisted to outbox.</returns>
        protected virtual async ValueTask PublishCore<T>(T message, PublishingRoute route, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
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

            if (IsOutboxEnabled())
            {
                await EnqueueOutboxAsync(payload, route, false, cancellationToken);
                return;
            }

            await _producingManager.PushAsync(payload, route, cancellationToken);
        }

        private async ValueTask PublishRawCore<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
        {
            if (IsOutboxEnabled())
            {
                await EnqueueOutboxAsync(message, route, true, cancellationToken);
                return;
            }

            await _producingManager.PushRawAsync(message, route, cancellationToken);
        }

        private async Task EnqueueOutboxAsync(object payload, PublishingRoute route, bool isRaw, CancellationToken cancellationToken)
        {
            if (_outboxStorage == null || _outboxMessageFactory == null)
                throw new InvalidOperationException("Pigeon outbox is enabled but no outbox storage has been registered.");

            var message = _outboxMessageFactory.Create(payload, route, isRaw);
            await _outboxStorage.AddAsync(message, cancellationToken);
            await _outboxStorage.SaveChangesAsync(cancellationToken);

            if (_outboxCommitNotifier != null)
                await _outboxCommitNotifier.NotifySavedAsync(message.Id, cancellationToken);
        }

        private bool IsOutboxEnabled()
            => _settings.Outbox?.Enabled == true;
    }
}
