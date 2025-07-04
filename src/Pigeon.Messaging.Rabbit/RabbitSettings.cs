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
        public string Url { get; init; }
    }
}
