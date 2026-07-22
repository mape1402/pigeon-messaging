namespace Pigeon.Messaging.Rabbit
{
    /// <summary>
    /// Represents the configuration settings required to connect to a RabbitMQ broker.
    /// </summary>
    public class RabbitSettings
    {
        /// <summary>
        /// Gets the connection URI used to establish a connection with the RabbitMQ broker.
        /// Example: amqp://user:password@host:port/vhost
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the RabbitMQ exchange used for routed publishing.
        /// When empty, Pigeon uses the default exchange and publishes directly to queues by topic.
        /// </summary>
        public string Exchange { get; set; }

        /// <summary>
        /// Gets or sets the RabbitMQ exchange type used when declaring <see cref="Exchange"/>.
        /// </summary>
        public string ExchangeType { get; set; } = "direct";

        /// <summary>
        /// Gets or sets whether the configured exchange should survive broker restarts.
        /// </summary>
        public bool DurableExchange { get; set; }
    }
}
