namespace Pigeon.Messaging.Kafka.Producing
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;

    /// <summary>
    /// Producing adapter for publishing messages to Kafka topics using a resolved IKafkaProducer.
    /// </summary>
    internal class KafkaProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<KafkaProducingAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="KafkaProducingAdapter"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider used to resolve Kafka producers.</param>
        /// <param name="logger">The logger for logging publishing operations and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public KafkaProducingAdapter(IServiceProvider serviceProvider, ILogger<KafkaProducingAdapter> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Publishes a wrapped payload message to the specified Kafka topic asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the message payload. Must be a class.</typeparam>
        /// <param name="payload">The wrapped payload containing the message and metadata.</param>
        /// <param name="topic">The Kafka topic to which the message will be published.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var kafkaProducer = _serviceProvider.GetRequiredService<IKafkaProducer<T>>();
                var result = await kafkaProducer.PublishAsync(payload, topic, cancellationToken);

                _logger.LogInformation("Kafka: Message published to topic '{Topic}' with offset {Offset} and partition {Partition}.",
                    topic, result.Offset, result.Partition);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message using Kafka Adapter.");
                throw;
            }
        }
    }
}
