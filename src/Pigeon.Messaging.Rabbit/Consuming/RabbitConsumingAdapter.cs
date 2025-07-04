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

    internal class RabbitConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly ILogger<RabbitConsumingAdapter> _logger;
        private readonly RabbitSettings _options;

        private IConnection _connection;
        private ConcurrentDictionary<string, IChannel> _channels = new();

        public RabbitConsumingAdapter(IConsumingConfigurator consumingConfigurator, IOptions<RabbitSettings> options, ILogger<RabbitConsumingAdapter> logger)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        public async ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            var factory = new ConnectionFactory { Uri = new Uri(_options.Url) };

            if(_connection == null || !_connection.IsOpen)
                _connection = await factory.CreateConnectionAsync(cancellationToken);

            var topics = _consumingConfigurator.GetAllTopics();

            foreach (var topic in topics)
            {
                var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                if (!_channels.TryAdd(topic, channel))
                {
                    await channel.DisposeAsync();
                    _logger.LogWarning($"RabbitConsumingAdapter: Consumer for topic '{topic}' already exists.");
                    continue;
                }

                await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                consumer.ReceivedAsync += (s, e) =>
                {
                    try
                    {
                        var body = e.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        if (MessageConsumed != null)
                            MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(e.RoutingKey, message));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "RabbitConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                    }

                    return Task.CompletedTask;
                };

                await channel.BasicConsumeAsync(topic, true, consumer, cancellationToken);

                _logger.LogInformation($"RabbitConsumingAdapter: Consumer for topic '{topic}' has been configured");
            }

            _logger.LogInformation("RabbitConsumingAdapter has been initialized");
        }

        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            foreach (var channel in _channels.Values)
            {
                if(channel.IsOpen)
                    await channel.CloseAsync(cancellationToken).ConfigureAwait(false);

                await channel.DisposeAsync();
            }

            _channels.Clear();

            if(_connection != null)
            {
                if(_connection.IsOpen)
                    await _connection.CloseAsync(cancellationToken).ConfigureAwait(false);

                await _connection.DisposeAsync();

                _connection = null;
            }

            _logger.LogInformation("RabbitConsumingAdapter has been stopped gracefully");
        }
    }
}
