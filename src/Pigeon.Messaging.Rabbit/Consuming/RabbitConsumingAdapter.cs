namespace Pigeon.Messaging.Rabbit.Consuming
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Adapter implementation for consuming messages from RabbitMQ queues using the new IChannel API.
    /// Manages multiple channels, one per topic, and dispatches received messages via the <see cref="MessageConsumed"/> event.
    /// </summary>
    internal class RabbitConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly GlobalSettings _globalSettings;
        private readonly RabbitSettings _settings;
        private readonly ILogger<RabbitConsumingAdapter> _logger;

        // Dictionary to keep track of open channels keyed by endpoint to avoid duplicate consumers
        private ConcurrentDictionary<string, IChannel> _channels = new();

        private readonly EventHandler<TopicEventArgs> _onTopicCreated;
        private readonly EventHandler<TopicEventArgs> _onTopicRemoved;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitConsumingAdapter"/> class.
        /// </summary>
        /// <param name="connectionProvider">Provider for RabbitMQ connections and channels.</param>
        /// <param name="consumingConfigurator">Configuration provider that supplies topics to consume.</param>
        /// <param name="globalSettings">Global messaging settings for domain and configuration.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public RabbitConsumingAdapter(IConnectionProvider connectionProvider, IConsumingConfigurator consumingConfigurator,
            IOptions<GlobalSettings> globalSettings, IOptions<RabbitSettings> settings, ILogger<RabbitConsumingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _globalSettings = globalSettings?.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _onTopicCreated = async (s, e) => await StartNewChannel(e.Endpoint, CancellationToken.None);
            _onTopicRemoved = async (s, e) => await StopChannel(e.Endpoint, CancellationToken.None);
        }

        /// <summary>
        /// Event raised when a message is consumed from any of the configured topics.
        /// </summary>
        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        /// <summary>
        /// Starts consuming messages asynchronously from all configured topics.
        /// Declares queues and configures consumers for each topic.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous start consume operation.</returns>
        public async ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated += _onTopicCreated;
            _consumingConfigurator.TopicRemoved += _onTopicRemoved;

            var endpoints = GetConfiguredEndpoints();

            foreach (var endpoint in endpoints)
                await StartNewChannel(endpoint, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("RabbitConsumingAdapter has been initialized");
        }

        /// <summary>
        /// Stops consuming messages by closing and disposing all open channels.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated -= _onTopicCreated;
            _consumingConfigurator.TopicRemoved -= _onTopicRemoved;

            foreach (var endpoint in _channels.Keys.Select(ParseEndpointKey))
                await StopChannel(endpoint, cancellationToken);

            _logger.LogInformation("RabbitConsumingAdapter has been stopped gracefully");
        }

        private async Task StartNewChannel(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            var channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

            if (!_channels.TryAdd(endpoint.Key, channel))
            {
                await channel.DisposeAsync();
                _logger.LogWarning("RabbitConsumingAdapter: Consumer for topic '{Topic}' and subscription '{Subscription}' already exists. Skipping creation.", endpoint.Topic, endpoint.Subscription);
                return;
            }

            await channel.QueueDeclareAsync(endpoint.ResourceName, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

            if (!string.IsNullOrWhiteSpace(_settings.Exchange))
            {
                await channel.ExchangeDeclareAsync(_settings.Exchange, _settings.ExchangeType, durable: _settings.DurableExchange, autoDelete: false, cancellationToken: cancellationToken);
                await channel.QueueBindAsync(endpoint.ResourceName, _settings.Exchange, endpoint.Topic, cancellationToken: cancellationToken);
            }

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += (s, e) =>
            {
                try
                {
                    var body = e.Body.ToArray();
                    var message = body.FromBytes();

                    MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(endpoint.Topic, message, endpoint.Subscription));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                }

                return Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(endpoint.ResourceName, autoAck: true, consumer, cancellationToken);

            _logger.LogInformation("RabbitConsumingAdapter: Consumer for topic '{Topic}' and subscription '{Subscription}' has been configured", endpoint.Topic, endpoint.Subscription);
        }

        private async Task StopChannel(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!_channels.TryRemove(endpoint.Key, out var channel))
                return;

            try
            {
                if (channel.IsOpen)
                    await channel.CloseAsync(cancellationToken).ConfigureAwait(false);

                await channel.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitConsumingAdapter: Error while stopping processor for topic '{Topic}' and subscription '{Subscription}'.", endpoint.Topic, endpoint.Subscription);
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
