namespace Pigeon.Messaging.Rabbit
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;
    using RabbitMQ.Client;

    internal class RabbitTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly RabbitSettings _settings;

        public RabbitTopologyAdapter(IConnectionProvider connectionProvider, IOptions<RabbitSettings> settings)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public string BrokerName => "RabbitMq";

        public async Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
        {
            await using var channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

            var exchange = ResolveExchange(route);

            if (!string.IsNullOrWhiteSpace(exchange))
            {
                await channel.ExchangeDeclareAsync(exchange, _settings.ExchangeType, durable: _settings.DurableExchange, autoDelete: false, cancellationToken: cancellationToken);
                return;
            }

            await channel.QueueDeclareAsync(route.Topic, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        }

        public async Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            await using var channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

            await channel.QueueDeclareAsync(endpoint.ResourceName, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(_settings.Exchange))
                return;

            await channel.ExchangeDeclareAsync(_settings.Exchange, _settings.ExchangeType, durable: _settings.DurableExchange, autoDelete: false, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(endpoint.ResourceName, _settings.Exchange, endpoint.Topic, cancellationToken: cancellationToken);
        }

        private string ResolveExchange(PublishingRoute route)
            => !string.IsNullOrWhiteSpace(route.Exchange)
                ? route.Exchange
                : _settings.Exchange ?? string.Empty;
    }
}
