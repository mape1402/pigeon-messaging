namespace Pigeon.Messaging.Kafka
{
    using Confluent.Kafka;

    /// <summary>
    /// Provides methods to retrieve Kafka producer and consumer configuration objects.
    /// </summary>
    public interface IConfigurationProvider
    {
        /// <summary>
        /// Gets the configuration settings for a Kafka producer.
        /// </summary>
        /// <returns>A <see cref="ProducerConfig"/> instance with the necessary settings for producing messages.</returns>
        ProducerConfig GetProducerConfig();

        /// <summary>
        /// Gets the configuration settings for a Kafka consumer.
        /// </summary>
        /// <returns>A <see cref="ConsumerConfig"/> instance with the necessary settings for consuming messages.</returns>
        ConsumerConfig GetConsumerConfig();
    }
}
