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

        public Producer(IEnumerable<IPublishInterceptor> interceptors, IProducingManager producingManager, IOptions<GlobalSettings> settings)
            : this(interceptors, producingManager, settings, null, null)
        {
        }

        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IOutboxStorage outboxStorage,
            OutboxMessageFactory outboxMessageFactory)
            : this(interceptors, producingManager, settings, outboxStorage, outboxMessageFactory, null)
        {
        }

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

        public virtual async ValueTask PublishAsync<T>(T message, string topic, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            await PublishCore(message, PublishingRoute.ForTopic(topic), version, cancellationToken);
        }

        public virtual async ValueTask PublishAsync<T>(T message, string exchange, string routingKey, SemanticVersion version, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            await PublishCore(message, PublishingRoute.ForExchange(exchange, routingKey), version, cancellationToken);
        }

        public virtual ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PublishAsync(message, topic, SemanticVersion.Default, cancellationToken);

        public virtual async ValueTask PublishRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));

            await PublishRawCore(message, PublishingRoute.ForTopic(topic), cancellationToken);
        }

        public virtual async ValueTask PublishRawAsync<T>(T message, string exchange, string routingKey, CancellationToken cancellationToken = default) where T : class
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            await PublishRawCore(message, PublishingRoute.ForExchange(exchange, routingKey), cancellationToken);
        }

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
