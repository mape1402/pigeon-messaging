namespace Pigeon.Messaging.Azure.EventHub.Producing
{
    using global::Azure.Messaging.EventHubs;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;

    /// <summary>
    /// Producing adapter for publishing messages to Azure Event Hubs using a custom event hub client.
    /// </summary>
    internal class EventHubProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly IEventHubProvider _eventHubProvider;
        private readonly ISerializer _serializer;
        private readonly ILogger<EventHubProducingAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHubProducingAdapter"/> class.
        /// </summary>
        /// <param name="eventHubProvider">The provider used to resolve Event Hub producers.</param>
        /// <param name="serializer">The serializer used for serializing messages to JSON.</param>
        /// <param name="logger">The logger for logging publishing operations and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public EventHubProducingAdapter(IEventHubProvider eventHubProvider, ISerializer serializer, ILogger<EventHubProducingAdapter> logger)
        {
            _eventHubProvider = eventHubProvider ?? throw new ArgumentNullException(nameof(eventHubProvider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Publishes a wrapped payload message to the specified Azure Event Hub asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the message payload. Must be a class.</typeparam>
        /// <param name="payload">The wrapped payload containing the message and metadata.</param>
        /// <param name="topic">The Event Hub name to which the message will be published.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var producer = _eventHubProvider.GetProducer(topic);
                var eventData = CreateEventData(payload);

                using var eventBatch = await producer.CreateBatchAsync(cancellationToken);
                
                if (!eventBatch.TryAdd(eventData))
                    throw new InvalidOperationException($"The event data for topic '{topic}' is too large to fit in a batch.");

                await producer.SendAsync(eventBatch, cancellationToken);

                _logger.LogInformation("EventHub: Message published to hub '{Topic}' successfully.", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message using Azure Event Hub Adapter.");
                throw;
            }
        }

        private EventData CreateEventData<T>(WrappedPayload<T> payload) where T : class
        {
            var bytes = _serializer.SerializeAsBytes(payload);
            var eventData = new EventData(bytes);

            // Add metadata as properties
            eventData.Properties.Add("Domain", payload.Domain);
            eventData.Properties.Add("MessageVersion", payload.MessageVersion.ToString());
            eventData.Properties.Add("CreatedOnUtc", payload.CreatedOnUtc.ToString("O"));

            return eventData;
        }
    }
}
