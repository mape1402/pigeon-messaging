namespace Pigeon.Messaging.Rabbit.Producing
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;
    using RabbitMQ.Client;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.Json;

    internal class RabbitProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly ILogger<RabbitProducingAdapter> _logger;
        private readonly ConcurrentDictionary<string, byte> _registeredTopics = new();
        private IChannel _channel; 
        private readonly SemaphoreSlim _channelLock = new(1, 1);

        public RabbitProducingAdapter(IConnectionProvider connectionProvider, ILogger<RabbitProducingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
            where T : class
        {
            await _channelLock.WaitAsync(cancellationToken);

            try
            {
                if (_channel == null || !_channel.IsOpen)
                    _channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

                if (_registeredTopics.TryAdd(topic, 0))
                    await _channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

                var payloadJson = JsonSerializer.Serialize(payload);
                var body = Encoding.UTF8.GetBytes(payloadJson);

                await _channel.BasicPublishAsync(string.Empty, topic, body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message using Rabbit Adapter.");
                throw;
            }
            finally
            {
                _channelLock.Release();
            }
        }
    }
}
