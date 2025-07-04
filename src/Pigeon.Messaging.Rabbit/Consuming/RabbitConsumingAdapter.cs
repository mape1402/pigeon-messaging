namespace Pigeon.Messaging.Rabbit.Consuming
{
    using Microsoft.Extensions.Logging;
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
        private readonly ILogger<RabbitConsumingAdapter> _logger;

        // Dictionary to keep track of open channels keyed by topic to avoid duplicate consumers
        private ConcurrentDictionary<string, IChannel> _channels = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitConsumingAdapter"/> class.
        /// </summary>
        /// <param name="connectionProvider">Provider for RabbitMQ connections and channels.</param>
        /// <param name="consumingConfigurator">Configuration provider that supplies topics to consume.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public RabbitConsumingAdapter(IConnectionProvider connectionProvider, IConsumingConfigurator consumingConfigurator, ILogger<RabbitConsumingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
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
            // Get all topics to consume from the configurator
            var topics = _consumingConfigurator.GetAllTopics();

            foreach (var topic in topics)
            {
                // Create a new channel for each topic
                var channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

                // Add channel to dictionary; if topic already exists, dispose the new channel and log warning
                if (!_channels.TryAdd(topic, channel))
                {
                    await channel.DisposeAsync();
                    _logger.LogWarning($"RabbitConsumingAdapter: Consumer for topic '{topic}' already exists.");
                    continue;
                }

                // Declare the queue to ensure it exists before consuming
                await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

                // Create an asynchronous event-based consumer
                var consumer = new AsyncEventingBasicConsumer(channel);

                // Register event handler to process received messages
                consumer.ReceivedAsync += (s, e) =>
                {
                    try
                    {
                        // Decode message body from bytes to UTF-8 string
                        var body = e.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        // Invoke the MessageConsumed event if there are subscribers
                        MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(e.RoutingKey, message));
                    }
                    catch (Exception ex)
                    {
                        // Log any error encountered during message processing
                        _logger.LogError(ex, "RabbitConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                    }

                    return Task.CompletedTask;
                };

                // Start consuming messages on the channel, auto-acknowledge messages
                await channel.BasicConsumeAsync(topic, autoAck: true, consumer, cancellationToken);

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
            // Close and dispose each channel if still open
            foreach (var channel in _channels.Values)
            {
                if (channel.IsOpen)
                    await channel.CloseAsync(cancellationToken).ConfigureAwait(false);

                await channel.DisposeAsync();
            }

            // Clear the channels dictionary
            _channels.Clear();

            _logger.LogInformation("RabbitConsumingAdapter has been stopped gracefully");
        }
    }
}
