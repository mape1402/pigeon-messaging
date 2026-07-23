namespace Pigeon.Messaging.Kafka
{
    using Confluent.Kafka;
    using Confluent.Kafka.Admin;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    internal class KafkaTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        private readonly IConfigurationProvider _configurationProvider;
        private readonly KafkaSettings _settings;

        public KafkaTopologyAdapter(IConfigurationProvider configurationProvider, IOptions<KafkaSettings> settings)
        {
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public string BrokerName => "Kafka";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => EnsureTopicAsync(route.Topic);

        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
            => EnsureTopicAsync(endpoint.Topic);

        private async Task EnsureTopicAsync(string topic)
        {
            using var adminClient = new AdminClientBuilder(_configurationProvider.GetProducerConfig()).Build();

            try
            {
                await adminClient.CreateTopicsAsync(new[]
                {
                    new TopicSpecification
                    {
                        Name = topic,
                        NumPartitions = _settings.TopicNumPartitions,
                        ReplicationFactor = _settings.TopicReplicationFactor
                    }
                });
            }
            catch (CreateTopicsException ex) when (ex.Results.Any(result => result.Error.Code == ErrorCode.TopicAlreadyExists))
            {
            }
        }
    }
}
