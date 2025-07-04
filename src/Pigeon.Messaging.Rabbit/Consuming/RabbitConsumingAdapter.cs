namespace Pigeon.Messaging.Rabbit.Consuming
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using System.Collections.Concurrent;
    using System.Text;

    internal class RabbitConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly ILogger<RabbitConsumingAdapter> _logger;

        private ConcurrentDictionary<string, IChannel> _channels = new();

        public RabbitConsumingAdapter(IConnectionProvider connectionProvider, IConsumingConfigurator consumingConfigurator, ILogger<RabbitConsumingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        public async ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            var topics = _consumingConfigurator.GetAllTopics();

            foreach (var topic in topics)
            {
                var channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

                if (!_channels.TryAdd(topic, channel))
                {
                    await channel.DisposeAsync();
                    _logger.LogWarning($"RabbitConsumingAdapter: Consumer for topic '{topic}' already exists.");
                    continue;
                }

                await channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

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

            _logger.LogInformation("RabbitConsumingAdapter has been stopped gracefully");
        }
    }
}
