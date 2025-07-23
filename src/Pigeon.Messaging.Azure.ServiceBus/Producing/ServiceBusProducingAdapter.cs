namespace Pigeon.Messaging.Azure.ServiceBus.Producing
{
    using global::Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Producing adapter for publishing messages to Azure Service Bus topics using a resolved IServiceBusProvider.
    /// </summary>
    internal class ServiceBusProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly IServiceBusProvider _serviceBusProvider;
        private readonly ILogger<ServiceBusProducingAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBusProducingAdapter"/> class.
        /// </summary>
        /// <param name="serviceBusProvider">The provider used to resolve Service Bus senders.</param>
        /// <param name="logger">The logger for logging publishing operations and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public ServiceBusProducingAdapter(IServiceBusProvider serviceBusProvider, ILogger<ServiceBusProducingAdapter> logger)
        {
            _serviceBusProvider = serviceBusProvider ?? throw new ArgumentNullException(nameof(serviceBusProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Publishes a wrapped payload message to the specified Azure Service Bus topic asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the message payload. Must be a class.</typeparam>
        /// <param name="payload">The wrapped payload containing the message and metadata.</param>
        /// <param name="topic">The Service Bus topic to which the message will be published.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var sender = _serviceBusProvider.GetSender(topic);

                var jsonOptions = new JsonSerializerOptions();
                jsonOptions.Converters.Add(new SemanticVersionJsonConverter());

                var json = JsonSerializer.Serialize(payload, jsonOptions);
                var bytes = Encoding.UTF8.GetBytes(json);

                ServiceBusMessage message = new(bytes);

                await sender.SendMessageAsync(message, cancellationToken);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message using Azure Service Bus Adapter.");
                throw;
            }
        }
    }
}
