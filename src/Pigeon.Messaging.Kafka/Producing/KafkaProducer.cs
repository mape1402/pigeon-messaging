namespace Pigeon.Messaging.Kafka.Producing
{
    using Confluent.Kafka;
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Kafka producer implementation for publishing wrapped payload messages to a Kafka topic.
    /// </summary>
    /// <typeparam name="T">The type of the message payload. Must be a class.</typeparam>
    internal class KafkaProducer<T> : IKafkaProducer<T> where T : class
    {
        private readonly IProducer<Null, WrappedPayload<T>> _producer;

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaProducer{T}"/> class using the specified configuration provider.
        /// </summary>
        /// <param name="configurationProvider">The provider for Kafka producer configuration settings.</param>
        /// <param name="serializer">The serializer used to serialize the wrapped payload.</param>
        public KafkaProducer(IConfigurationProvider configurationProvider, ISerializer serializer)
        {
            var config = configurationProvider.GetProducerConfig();

            _producer = new ProducerBuilder<Null, WrappedPayload<T>>(config)
                .SetValueSerializer(new JsonSerializer<WrappedPayload<T>>(serializer))
                .Build();
        }

        /// <summary>
        /// Publishes a wrapped payload message to the specified Kafka topic asynchronously.
        /// </summary>
        /// <param name="payload">The wrapped payload containing the message and metadata.</param>
        /// <param name="topic">The Kafka topic to which the message will be published.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous publish operation. The task result contains the Kafka delivery result.</returns>
        public Task<DeliveryResult<Null, WrappedPayload<T>>> PublishAsync(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
        {
            var message = new Message<Null, WrappedPayload<T>>
            {
                Value = payload
            };

            return _producer.ProduceAsync(topic, message, cancellationToken);
        }
    }
}
