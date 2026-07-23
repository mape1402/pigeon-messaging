namespace Pigeon.Messaging.Kafka.Consuming
{
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Topology;
    using System.Collections.Concurrent;
    using System.Threading;

    /// <summary>
    /// Adapter for consuming messages from Kafka topics. Manages consumers and listeners for each topic,
    /// and raises events when messages are consumed.
    /// </summary>
    internal class KafkaConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IConfigurationProvider _configurationProvider;
        private readonly ITopologyProvisioningService _topologyProvisioningService;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<KafkaConsumingAdapter> _logger;

        private readonly ConcurrentDictionary<string, Task> _listeners = new();
        private readonly ConcurrentDictionary<string, IConsumer<Ignore, string>> _consumers = new();
        private CancellationTokenSource _cancellationTokenSource;

        private readonly EventHandler<TopicEventArgs> _onTopicCreated;
        private readonly EventHandler<TopicEventArgs> _onTopicRemoved;

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaConsumingAdapter"/> class.
        /// </summary>
        /// <param name="consumingConfigurator">The configurator for retrieving topics to consume.</param>
        /// <param name="configurationProvider">The provider for Kafka consumer configuration.</param>
        /// <param name="globalSettings">Global messaging settings for domain and configuration.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public KafkaConsumingAdapter(IConsumingConfigurator consumingConfigurator, IConfigurationProvider configurationProvider,
            IOptions<GlobalSettings> globalSettings, ILogger<KafkaConsumingAdapter> logger)
            : this(consumingConfigurator, configurationProvider, NoopTopologyProvisioningService.Instance, globalSettings, logger)
        {
        }

        public KafkaConsumingAdapter(IConsumingConfigurator consumingConfigurator, IConfigurationProvider configurationProvider,
            ITopologyProvisioningService topologyProvisioningService, IOptions<GlobalSettings> globalSettings, ILogger<KafkaConsumingAdapter> logger)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
            _globalSettings = globalSettings.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _onTopicCreated = async (s, e) => await StartNewConsumer(e.Endpoint, CancellationToken.None);
            _onTopicRemoved = (s, e) => StopConsumer(e.Endpoint);
        }

        /// <summary>
        /// Event raised when a message is consumed from any of the configured topics.
        /// </summary>
        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        /// <summary>
        /// Starts consuming messages asynchronously from all configured topics.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous start operation.</returns>
        public async ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated += _onTopicCreated;
            _consumingConfigurator.TopicRemoved += _onTopicRemoved;

            var endpoints = GetConfiguredEndpoints();
            var cts = new CancellationTokenSource();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            foreach (var endpoint in endpoints)
                await StartNewConsumer(endpoint, cancellationToken);

            _logger.LogInformation("KafkaConsumingAdapter has been initialized");
        }

        /// <summary>
        /// Stops consuming messages by cancelling listeners and disposing all consumers.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated -= _onTopicCreated;
            _consumingConfigurator.TopicRemoved -= _onTopicRemoved;

            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            try
            {
                await Task.WhenAll(_listeners.Values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KafkaConsumingAdapter: Unexpected error while waiting for Kafka listeners to stop.");
            }

            foreach (var endpoint in _consumers.Keys.Select(ParseEndpointKey))
                StopConsumer(endpoint);

            _consumers.Clear();
            _listeners.Clear();

            _logger.LogInformation("KafkaConsumingAdapter has been stopped gracefully");
        }

        /// <summary>
        /// Listens for messages on the given Kafka consumer and raises the MessageConsumed event when a message is received.
        /// </summary>
        /// <param name="consumer">The Kafka consumer to listen on.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for messages.</param>
        private void Listen(IConsumer<Ignore, string> consumer, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(cancellationToken);
                    MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(result.Topic, result.Message.Value));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "KafkaConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                }
            }
        }

        private void ListenEndpoint(IConsumer<Ignore, string> consumer, ConsumerEndpoint endpoint, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(cancellationToken);
                    var topic = string.IsNullOrWhiteSpace(endpoint.Topic) ? result.Topic : endpoint.Topic;
                    MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(topic, result.Message.Value, endpoint.Subscription));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "KafkaConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                }
            }
        }

        private async Task StartNewConsumer(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            await _topologyProvisioningService.EnsureConsumeTopologyAsync(endpoint, cancellationToken);

            var config = endpoint.Subscription == ConsumerEndpoint.DefaultSubscription
                ? _configurationProvider.GetConsumerConfig()
                : _configurationProvider.GetConsumerConfig(endpoint.Subscription);
            var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

            if (!_consumers.TryAdd(endpoint.Key, consumer))
            {
                consumer.Dispose();
                _logger.LogWarning("KafkaConsumingAdapter: Consumer for topic '{Topic}' and subscription '{Subscription}' already exists. Skipping creation.", endpoint.Topic, endpoint.Subscription);
                return;
            }

            consumer.Subscribe(endpoint.Topic);

            var listener = Task.Run(() => ListenEndpoint(consumer, endpoint, _cancellationTokenSource.Token));

            _listeners.TryAdd(endpoint.Key, listener);
        }

        private void StopConsumer(ConsumerEndpoint endpoint)
        {
            if (!_consumers.TryRemove(endpoint.Key, out var consumer))
                return;

            try
            {
                consumer.Close();
                consumer.Dispose();

                _listeners[endpoint.Key] = default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KafkaConsumingAdapter: Error while stopping processor for topic '{Topic}' and subscription '{Subscription}'", endpoint.Topic, endpoint.Subscription);
            }
        }

        private static ConsumerEndpoint ParseEndpointKey(string key)
        {
            var parts = key.Split("::", 2, StringSplitOptions.None);
            return new ConsumerEndpoint(parts[0], parts.Length > 1 ? parts[1] : ConsumerEndpoint.DefaultSubscription);
        }

        private IEnumerable<ConsumerEndpoint> GetConfiguredEndpoints()
        {
            var endpoints = _consumingConfigurator.GetAllEndpoints()?.ToArray();

            if (endpoints is { Length: > 0 })
                return endpoints;

            return _consumingConfigurator.GetAllTopics()?.Select(topic => new ConsumerEndpoint(topic)) ?? Enumerable.Empty<ConsumerEndpoint>();
        }
    }
}
