namespace Pigeon.Messaging.Azure.EventGrid.Producing
{
    using global::Azure.Messaging.EventGrid;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;

    /// <summary>
    /// Producing adapter for publishing messages to Azure Event Grid using a custom event grid client.
    /// </summary>
    internal class EventGridProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly IEventGridProvider _eventGridProvider;
        private readonly ISerializer _serializer;
        private readonly ILogger<EventGridProducingAdapter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridProducingAdapter"/> class.
        /// </summary>
        /// <param name="eventGridProvider">The provider used to resolve Event Grid clients.</param>
        /// <param name="serializer">The serializer used for serializing messages to JSON.</param>
        /// <param name="logger">The logger for logging publishing operations and errors.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public EventGridProducingAdapter(IEventGridProvider eventGridProvider, ISerializer serializer, ILogger<EventGridProducingAdapter> logger)
        {
            _eventGridProvider = eventGridProvider ?? throw new ArgumentNullException(nameof(eventGridProvider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Publishes a wrapped payload message to the specified Azure Event Grid topic asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the message payload. Must be a class.</typeparam>
        /// <param name="payload">The wrapped payload containing the message and metadata.</param>
        /// <param name="topic">The Event Grid topic to which the message will be published.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous publish operation.</returns>
        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var client = _eventGridProvider.GetClient(topic);
                var eventData = CreateEventGridData(payload, topic);

                await client.PublishCloudEventsAsync([ eventData ], cancellationToken);

                _logger.LogInformation("EventGrid: Message published to topic '{Topic}' successfully.", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message using Azure Event Grid Adapter.");
                throw;
            }
        }

        private EventGridEvent CreateEventGridData<T>(WrappedPayload<T> payload, string topic) where T : class
        {
            var jsonData = _serializer.Serialize(payload);
            
            return new EventGridEvent(
                subject: topic,
                eventType: $"{topic}.{payload.MessageVersion}",
                dataVersion: payload.MessageVersion.ToString(),
                data: jsonData)
            {
                Id = Guid.NewGuid().ToString(),
                EventTime = payload.CreatedOnUtc
            };
        }
    }
}
