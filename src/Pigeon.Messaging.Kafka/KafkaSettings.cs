namespace Pigeon.Messaging.Kafka
{
    using Confluent.Kafka;

    /// <summary>
    /// Represents configuration settings for connecting to a Kafka cluster, including authentication and protocol options.
    /// </summary>
    public sealed class KafkaSettings
    {
        /// <summary>
        /// Gets or sets the Kafka bootstrap servers (comma-separated host:port pairs).
        /// </summary>
        public string BootstrapServers { get; set; }

        /// <summary>
        /// Gets or sets the username for authenticating with Kafka (if required).
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Gets or sets the password for authenticating with Kafka (if required).
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Gets or sets the security protocol used for Kafka connections. Default is <see cref="SecurityProtocol.SaslSsl"/>.
        /// </summary>
        public SecurityProtocol SecurityProtocol { get; set; } = SecurityProtocol.SaslSsl;

        /// <summary>
        /// Gets or sets the SASL mechanism used for authentication. Default is <see cref="SaslMechanism.Plain"/>.
        /// </summary>
        public SaslMechanism SaslMechanism { get; set; } = SaslMechanism.Plain;

        /// <summary>
        /// Gets or sets the required acknowledgments for Kafka producer requests. Default is <see cref="Acks.All"/>.
        /// </summary>
        public Acks Acks { get; set; } = Acks.All;
    }
}
