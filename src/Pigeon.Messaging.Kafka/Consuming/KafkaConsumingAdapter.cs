namespace Pigeon.Messaging.Kafka.Consuming
{
    using Confluent.Kafka;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
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
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<KafkaConsumingAdapter> _logger;

        private readonly ConcurrentDictionary<string, Task> _listeners = new();
        private readonly ConcurrentDictionary<string, IConsumer<Ignore, string>> _consumers = new();
        private CancellationTokenSource _cancellationTokenSource;

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
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
            _globalSettings = globalSettings.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        public ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            var topics = _consumingConfigurator.GetAllTopics();
            _cancellationTokenSource = new CancellationTokenSource();
            var linkedcts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token);

            foreach (var topic in topics)
            {
                var config = _configurationProvider.GetConsumerConfig();
                var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

                if(!_consumers.TryAdd(topic, consumer))
                {
                    consumer.Dispose();
                    _logger.LogWarning("KafkaConsumingAdapter: Consumer for topic '{Topic}' already exists. Skipping creation.", topic);
                    continue;
                }

                consumer.Subscribe($"{_globalSettings.Domain}.{topic}");

                var listener = Task.Run(() => Listen(consumer, linkedcts.Token));

                _listeners.TryAdd(topic, listener);
            }

            _logger.LogInformation("KafkaConsumingAdapter has been initialized");

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Stops consuming messages by cancelling listeners and disposing all consumers.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            if(_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            try
            {
                await Task.WhenAll(_listeners.Values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KafkaConsumingAdapter: Unexpected error while waiting for Kafka listeners to stop.");
            }

            foreach (var topic in _consumers.Keys)
            {
                try
                {
                    _consumers[topic].Close();
                    _consumers[topic].Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"KafkaConsumingAdapter: Error while stopping processor for topic."); 
                }

                _listeners[topic] = default;
            }

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
    }
}
