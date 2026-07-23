namespace Pigeon.Messaging.Kafka
{
    using Confluent.Kafka;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Management;

    /// <summary>
    /// Provides Kafka producer and consumer configuration based on global and Kafka-specific settings.
    /// </summary>
    internal class ConfigurationProvider : IConfigurationProvider
    {
        private readonly GlobalSettings _globalSettings;
        private readonly KafkaSettings _kafkaSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationProvider"/> class.
        /// </summary>
        /// <param name="globalSettings">Global messaging settings, including domain information.</param>
        /// <param name="kafkaSettings">Kafka-specific connection and authentication settings.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public ConfigurationProvider(IOptions<GlobalSettings> globalSettings, IOptions<KafkaSettings> kafkaSettings)
        {
            _globalSettings = globalSettings.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _kafkaSettings = kafkaSettings.Value ?? throw new ArgumentNullException(nameof(kafkaSettings));
        }

        /// <inheritdoc/>
        public ConsumerConfig GetConsumerConfig()
            => GetConsumerConfig(_globalSettings.Domain);

        public ConsumerConfig GetConsumerConfig(string consumerGroup)
        {
            return new ConsumerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                GroupId = string.IsNullOrWhiteSpace(consumerGroup) ? _globalSettings.Domain : consumerGroup,
                SaslUsername = _kafkaSettings.UserName,
                SaslPassword = _kafkaSettings.Password,
                SecurityProtocol = _kafkaSettings.SecurityProtocol,
                SaslMechanism = _kafkaSettings.SaslMechanism,
                Acks = _kafkaSettings.Acks,
                EnableAutoCommit = _globalSettings.ConsumerExecution?.AcknowledgementMode == MessageAcknowledgementMode.OnReceive,
            };
        }

        /// <inheritdoc/>
        public ProducerConfig GetProducerConfig()
        {
            return new ProducerConfig
            {
                BootstrapServers = _kafkaSettings.BootstrapServers,
                SaslUsername = _kafkaSettings.UserName,
                SaslPassword = _kafkaSettings.Password,
                SecurityProtocol = _kafkaSettings.SecurityProtocol,
                SaslMechanism = _kafkaSettings.SaslMechanism,
                Acks = _kafkaSettings.Acks
            };
        }
    }
}
