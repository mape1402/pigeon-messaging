namespace Pigeon.Messaging.Rabbit.Consuming
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using System.Collections.Concurrent;
    using System.Text;
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
        private readonly ILogger<RabbitConsumingAdapter> _logger;

        // Dictionary to keep track of open channels keyed by topic to avoid duplicate consumers
        private ConcurrentDictionary<string, IChannel> _channels = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitConsumingAdapter"/> class.
        /// </summary>
        /// <param name="connectionProvider">Provider for RabbitMQ connections and channels.</param>
        /// <param name="consumingConfigurator">Configuration provider that supplies topics to consume.</param>
        /// <param name="globalSettings">Global messaging settings for domain and configuration.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public RabbitConsumingAdapter(IConnectionProvider connectionProvider, IConsumingConfigurator consumingConfigurator, 
            IOptions<GlobalSettings> globalSettings, ILogger<RabbitConsumingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _globalSettings = globalSettings?.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            var topics = _consumingConfigurator.GetAllTopics();

            foreach (var topic in topics)
            {
                var channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

                if (!_channels.TryAdd(topic, channel))
                {
                    await channel.DisposeAsync();
                    _logger.LogWarning("RabbitConsumingAdapter: Consumer for topic '{Topic}' already exists. Skipping creation.", topic);
                    continue;
                }

                await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += (s, e) =>
                {
                    try
                    {
                        var body = e.Body.ToArray();
                        var message = body.FromBytes();

                        MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(e.RoutingKey, message));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RabbitConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                    }

                    return Task.CompletedTask;
                };

                await channel.BasicConsumeAsync($"{_globalSettings.Domain}.{topic}", autoAck: true, consumer, cancellationToken);

                _logger.LogInformation($"RabbitConsumingAdapter: Consumer for topic '{topic}' has been configured");
            }

            _logger.LogInformation("RabbitConsumingAdapter has been initialized");
        }

        /// <summary>
        /// Stops consuming messages by closing and disposing all open channels.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A task representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var channel in _channels.Values)
            {
                try
                {
                    if (channel.IsOpen)
                        await channel.CloseAsync(cancellationToken).ConfigureAwait(false);

                    await channel.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RabbitConsumingAdapter: Error while stopping processor for topic.");
                }
            }

            _channels.Clear();

            _logger.LogInformation("RabbitConsumingAdapter has been stopped gracefully");
        }
    }
}
