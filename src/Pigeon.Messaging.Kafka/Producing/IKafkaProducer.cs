namespace Pigeon.Messaging.Kafka.Producing
{
    using Confluent.Kafka;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Defines a contract for a Kafka producer capable of publishing wrapped payload messages to a Kafka topic.
    /// </summary>
    /// <typeparam name="T">The type of the message payload. Must be a class.</typeparam>
    public interface IKafkaProducer<T> where T : class
    {
        /// <summary>
        /// Publishes a wrapped payload message to the specified Kafka topic asynchronously.
        /// </summary>
        /// <param name="payload">The wrapped payload containing the message and metadata.</param>
        /// <param name="topic">The Kafka topic to which the message will be published.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous publish operation. The task result contains the Kafka delivery result.</returns>
        Task<DeliveryResult<Null, WrappedPayload<T>>> PublishAsync(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default);
    }
}
