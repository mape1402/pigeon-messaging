namespace Pigeon.Messaging.Producing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Mule;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing.Management;
    using System.Transactions;

    /// <summary>
    /// Provides a base implementation for a message producer with support for publish interceptors,
    /// payload wrapping, versioning, and optional transactional outbox persistence.
    /// </summary>
    public class Producer : IProducer
    {
        private readonly IEnumerable<IPublishInterceptor> _interceptors;
        private readonly IEnumerable<IPublishDecisionInterceptor> _decisionInterceptors;
        private readonly IProducingManager _producingManager;
        private readonly GlobalSettings _settings;
        private readonly IOutboxStorage _outboxStorage;
        private readonly OutboxMessageFactory _outboxMessageFactory;
        private readonly IOutboxCommitNotifier _outboxCommitNotifier;
        private readonly IMuleClient _muleClient;
        private readonly PigeonRouteInterceptorRegistry _routeInterceptorRegistry;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Initializes a new producer without outbox persistence.
        /// </summary>
        /// <param name="interceptors">The publish interceptors executed before wrapped publishing.</param>
        /// <param name="producingManager">The producing manager that dispatches messages to broker adapters.</param>
        /// <param name="settings">The global Pigeon settings.</param>
        /// <param name="decisionInterceptors">The publish decision interceptors executed before broker or outbox dispatch.</param>
        /// <param name="routeInterceptorRegistry">The route-specific interceptor registry.</param>
        /// <param name="serviceProvider">The current scoped service provider.</param>
        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IEnumerable<IPublishDecisionInterceptor> decisionInterceptors = null,
            PigeonRouteInterceptorRegistry routeInterceptorRegistry = null,
            IServiceProvider serviceProvider = null)
            : this(interceptors, producingManager, settings, (OutboxMessageFactory)null, (IMuleClient)null, decisionInterceptors, routeInterceptorRegistry, serviceProvider)
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
        /// <param name="decisionInterceptors">The publish decision interceptors executed before broker or outbox dispatch.</param>
        /// <param name="routeInterceptorRegistry">The route-specific interceptor registry.</param>
        /// <param name="serviceProvider">The current scoped service provider.</param>
        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IOutboxStorage outboxStorage,
            OutboxMessageFactory outboxMessageFactory,
            IEnumerable<IPublishDecisionInterceptor> decisionInterceptors = null,
            PigeonRouteInterceptorRegistry routeInterceptorRegistry = null,
            IServiceProvider serviceProvider = null)
            : this(interceptors, producingManager, settings, outboxStorage, outboxMessageFactory, null, null, decisionInterceptors, routeInterceptorRegistry, serviceProvider)
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
        /// <param name="decisionInterceptors">The publish decision interceptors executed before broker or outbox dispatch.</param>
        /// <param name="routeInterceptorRegistry">The route-specific interceptor registry.</param>
        /// <param name="serviceProvider">The current scoped service provider.</param>
        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IOutboxStorage outboxStorage,
            OutboxMessageFactory outboxMessageFactory,
            IOutboxCommitNotifier outboxCommitNotifier,
            IEnumerable<IPublishDecisionInterceptor> decisionInterceptors = null,
            PigeonRouteInterceptorRegistry routeInterceptorRegistry = null,
            IServiceProvider serviceProvider = null)
            : this(interceptors, producingManager, settings, outboxStorage, outboxMessageFactory, outboxCommitNotifier, null, decisionInterceptors, routeInterceptorRegistry, serviceProvider)
        {
        }

        /// <summary>
        /// Initializes a new producer with Mule-backed outbox persistence.
        /// </summary>
        /// <param name="interceptors">The publish interceptors executed before wrapped publishing.</param>
        /// <param name="producingManager">The producing manager that dispatches messages to broker adapters.</param>
        /// <param name="settings">The global Pigeon settings.</param>
        /// <param name="outboxMessageFactory">The factory that creates persisted outbox messages.</param>
        /// <param name="muleClient">The durable action client used when outbox is enabled.</param>
        /// <param name="decisionInterceptors">The publish decision interceptors executed before broker or outbox dispatch.</param>
        /// <param name="routeInterceptorRegistry">The route-specific interceptor registry.</param>
        /// <param name="serviceProvider">The current scoped service provider.</param>
        public Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            OutboxMessageFactory outboxMessageFactory,
            IMuleClient muleClient,
            IEnumerable<IPublishDecisionInterceptor> decisionInterceptors = null,
            PigeonRouteInterceptorRegistry routeInterceptorRegistry = null,
            IServiceProvider serviceProvider = null)
            : this(interceptors, producingManager, settings, null, outboxMessageFactory, null, muleClient, decisionInterceptors, routeInterceptorRegistry, serviceProvider)
        {
        }

        private Producer(
            IEnumerable<IPublishInterceptor> interceptors,
            IProducingManager producingManager,
            IOptions<GlobalSettings> settings,
            IOutboxStorage outboxStorage,
            OutboxMessageFactory outboxMessageFactory,
            IOutboxCommitNotifier outboxCommitNotifier,
            IMuleClient muleClient,
            IEnumerable<IPublishDecisionInterceptor> decisionInterceptors,
            PigeonRouteInterceptorRegistry routeInterceptorRegistry,
            IServiceProvider serviceProvider)
        {
            _interceptors = interceptors ?? throw new ArgumentNullException(nameof(interceptors));
            _decisionInterceptors = decisionInterceptors ?? Array.Empty<IPublishDecisionInterceptor>();
            _producingManager = producingManager ?? throw new ArgumentNullException(nameof(producingManager));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _outboxStorage = outboxStorage;
            _outboxMessageFactory = outboxMessageFactory;
            _outboxCommitNotifier = outboxCommitNotifier;
            _muleClient = muleClient;
            _routeInterceptorRegistry = routeInterceptorRegistry;
            _serviceProvider = serviceProvider;
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
            var publishContext = new PublishContext
            {
                IsRaw = false,
                Message = message,
                MessageType = typeof(T),
                Route = route,
                Version = version
            };

            foreach (var interceptor in _interceptors)
                await interceptor.Intercept(publishContext, cancellationToken);

            var decision = await GetPublishDecisionAsync(publishContext, cancellationToken);
            publishContext.Route = decision.Route ?? publishContext.Route;
            publishContext.MergeMetadata(decision.Metadata);

            if (await ApplyPublishDecisionAsync(
                    decision,
                    () => EnqueueOutboxAsync(CreatePayload(message, publishContext, version), publishContext.Route, false, cancellationToken),
                    () => PublishDirectAsync(() => _producingManager.PushAsync(
                        CreatePayload(message, publishContext, version),
                        publishContext.Route,
                        cancellationToken))))
                return;

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
                await EnqueueOutboxAsync(payload, publishContext.Route, false, cancellationToken);
                return;
            }

            await PublishDirectAsync(
                () => _producingManager.PushAsync(payload, publishContext.Route, cancellationToken));
        }

        private async ValueTask PublishRawCore<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
        {
            var publishContext = new PublishContext
            {
                IsRaw = true,
                Message = message,
                MessageType = typeof(T),
                Route = route,
                Version = SemanticVersion.Default
            };

            var decision = await GetPublishDecisionAsync(publishContext, cancellationToken);
            publishContext.Route = decision.Route ?? publishContext.Route;
            publishContext.MergeMetadata(decision.Metadata);

            if (await ApplyPublishDecisionAsync(
                    decision,
                    () => EnqueueOutboxAsync(message, publishContext.Route, true, cancellationToken),
                    () => PublishDirectAsync(() => _producingManager.PushRawAsync(message, publishContext.Route, cancellationToken))))
                return;

            if (IsOutboxEnabled())
            {
                await EnqueueOutboxAsync(message, publishContext.Route, true, cancellationToken);
                return;
            }

            await PublishDirectAsync(
                () => _producingManager.PushRawAsync(message, publishContext.Route, cancellationToken));
        }

        private WrappedPayload<T> CreatePayload<T>(T message, PublishContext publishContext, SemanticVersion version)
            where T : class
            => new()
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Message = message,
                MessageVersion = version,
                Metadata = publishContext.GetMetadata(),
                Domain = _settings.Domain
            };

        private async ValueTask<PigeonPublishDecisionResult> GetPublishDecisionAsync(
            PublishContext publishContext,
            CancellationToken cancellationToken)
        {
            foreach (var interceptor in _decisionInterceptors)
            {
                var decision = await interceptor.InterceptAsync(publishContext, cancellationToken)
                    ?? PigeonPublishDecisionResult.Continue;

                publishContext.MergeMetadata(decision.Metadata);

                if (decision.Decision != PigeonPublishDecision.Continue)
                    return decision;
            }

            if (_routeInterceptorRegistry == null)
                return PigeonPublishDecisionResult.Continue;

            var route = new PigeonRouteKey(publishContext.Route.Topic, publishContext.Version);
            foreach (var interceptorType in _routeInterceptorRegistry.GetPublishDecisionInterceptors(route))
            {
                if (_serviceProvider == null)
                    continue;

                var interceptor = (IPublishDecisionInterceptor)_serviceProvider.GetRequiredService(interceptorType);
                var decision = await interceptor.InterceptAsync(publishContext, cancellationToken)
                    ?? PigeonPublishDecisionResult.Continue;

                publishContext.MergeMetadata(decision.Metadata);

                if (decision.Decision != PigeonPublishDecision.Continue)
                    return decision;
            }

            return PigeonPublishDecisionResult.Continue;
        }

        private async ValueTask<bool> ApplyPublishDecisionAsync(
            PigeonPublishDecisionResult decision,
            Func<Task> enqueueOutboxAsync,
            Func<ValueTask> publishNowAsync)
        {
            switch (decision.Decision)
            {
                case PigeonPublishDecision.Skip:
                    return true;
                case PigeonPublishDecision.Reject:
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(decision.Reason)
                            ? "The publish operation was rejected by a Pigeon publish decision interceptor."
                            : decision.Reason);
                case PigeonPublishDecision.UseOutbox:
                    await enqueueOutboxAsync();
                    return true;
                case PigeonPublishDecision.PublishNow:
                    await publishNowAsync();
                    return true;
                case PigeonPublishDecision.Continue:
                default:
                    return false;
            }
        }

        private async Task EnqueueOutboxAsync(object payload, PublishingRoute route, bool isRaw, CancellationToken cancellationToken)
        {
            if (_muleClient == null || _outboxMessageFactory == null)
            {
                await EnqueueLegacyOutboxAsync(payload, route, isRaw, cancellationToken);
                return;
            }

            var message = _outboxMessageFactory.Create(payload, route, isRaw);
            await _muleClient.EnqueueAsync(PigeonOutboxActionKeys.Publish, message, cancellationToken);
        }

        private async Task EnqueueLegacyOutboxAsync(object payload, PublishingRoute route, bool isRaw, CancellationToken cancellationToken)
        {
            if (_outboxStorage == null || _outboxMessageFactory == null)
                throw new InvalidOperationException("Pigeon outbox is enabled but Mule durable actions have not been registered.");

            var message = _outboxMessageFactory.Create(payload, route, isRaw);
            await _outboxStorage.AddAsync(message, cancellationToken);
            await _outboxStorage.SaveChangesAsync(cancellationToken);

            if (_outboxCommitNotifier != null)
                await _outboxCommitNotifier.NotifySavedAsync(message.Id, cancellationToken);
        }

        private bool IsOutboxEnabled()
            => _settings.Outbox?.Enabled == true;

        private async ValueTask PublishDirectAsync(Func<ValueTask> publish)
        {
            if (publish == null)
                throw new ArgumentNullException(nameof(publish));

            if (Transaction.Current == null)
            {
                await publish();
                return;
            }

            var behavior = _settings.Publishing?.AmbientTransactionBehavior
                ?? AmbientTransactionPublishBehavior.SuppressTransaction;

            if (behavior == AmbientTransactionPublishBehavior.Throw)
                throw new InvalidOperationException("Direct broker publishing cannot run inside an ambient transaction with the current Pigeon publishing settings.");

            using var scope = new TransactionScope(
                TransactionScopeOption.Suppress,
                TransactionScopeAsyncFlowOption.Enabled);

            await publish();

            scope.Complete();
        }
    }
}
