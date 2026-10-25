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
        private readonly Func<ProducerConfig, IAdminClient> _createAdminClient;

        public KafkaTopologyAdapter(IConfigurationProvider configurationProvider, IOptions<KafkaSettings> settings)
            : this(configurationProvider, settings, config => new AdminClientBuilder(config).Build())
        {
        }

        internal KafkaTopologyAdapter(IConfigurationProvider configurationProvider, IOptions<KafkaSettings> settings, Func<ProducerConfig, IAdminClient> createAdminClient)
        {
            _configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _createAdminClient = createAdminClient ?? throw new ArgumentNullException(nameof(createAdminClient));
        }

        public string BrokerName => "Kafka";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => EnsureTopicAsync(route.Topic);

        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
            => EnsureTopicAsync(endpoint.Topic);

        private async Task EnsureTopicAsync(string topic)
        {
            using var adminClient = _createAdminClient(_configurationProvider.GetProducerConfig());

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
