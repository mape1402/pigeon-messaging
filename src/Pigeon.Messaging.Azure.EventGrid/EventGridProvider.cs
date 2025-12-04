namespace Pigeon.Messaging.Azure.EventGrid
{
    using global::Azure;
    using global::Azure.Messaging.EventGrid;
    using global::Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Options;
    using System.Collections.Concurrent;

    /// <summary>
    /// Provides methods to interact with Azure Event Grid, allowing the creation of clients and subscriptions for messaging operations.
    /// </summary>
    public interface IEventGridProvider
    {
        /// <summary>
        /// Gets an Event Grid publisher client for the specified topic.
        /// </summary>
        /// <param name="topic">The name of the topic.</param>
        /// <returns>An Event Grid publisher client.</returns>
        IEventGridPublisher GetClient(string topic);

        /// <summary>
        /// Creates a <see cref="ServiceBusProcessor"/> for processing messages from the specified topic.
        /// </summary>
        /// <param name="topic">The name of the topic from which messages will be processed. Cannot be null or empty.</param>
        /// <returns>A <see cref="ServiceBusProcessor"/> configured to process messages from the specified topic.</returns>
        ServiceBusProcessor CreateProcessor(string topic);
    }

    /// <summary>
    /// Defines a contract for publishing cloud events to Event Grid.
    /// </summary>
    public interface IEventGridPublisher
    {
        /// <summary>
        /// Publishes cloud events to Event Grid.
        /// </summary>
        /// <param name="events">The cloud events to publish.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task PublishCloudEventsAsync(IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides methods to interact with Azure Event Grid, allowing the creation of clients and subscriptions for messaging operations.
    /// </summary>
    internal class EventGridProvider : IEventGridProvider
    {
        private readonly ServiceBusClient _client;
        private readonly AzureEventGridSettings _settings;
        private readonly ConcurrentDictionary<string, IEventGridPublisher> _publisherClients = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridProvider"/> class.
        /// </summary>
        /// <param name="options">The Azure Event Grid settings options.</param>
        public EventGridProvider(IOptions<AzureEventGridSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _client = new ServiceBusClient(options.Value.ServiceBusEndPoint);
        }

        /// <inheritdoc />
        public IEventGridPublisher GetClient(string topic)
        {
            return _publisherClients.GetOrAdd(topic, _ =>
            {
                var routingKey = string.Empty;
                if (_settings.TopicRouting == null || !_settings.TopicRouting.TryGetValue(topic, out routingKey))
                {
                    if(string.IsNullOrWhiteSpace(_settings.DefaultEndpoint))
                        throw new InvalidOperationException($"No routing key found for topic '{topic}', and no default endpoint is configured.");

                    routingKey = _settings.DefaultEndpoint;
                }

                if (!_settings.Endpoints.TryGetValue(routingKey, out var endpoint))
                    throw new InvalidOperationException($"Event Grid topic '{topic}' is not configured in the settings. Please ensure the topic is defined in the Endpoints configuration.");

                var client = new EventGridPublisherClient(new Uri(endpoint.Url), new AzureKeyCredential(endpoint.AccessKey));
                return new EventGridPublisher(_settings, topic, client);
            });
        }

        /// <inheritdoc />
        public ServiceBusProcessor CreateProcessor(string topic)
            => _client.CreateProcessor(topic, new ServiceBusProcessorOptions());
    }

    /// <summary>
    /// Event Grid publisher implementation.
    /// </summary>
    internal class EventGridPublisher : IEventGridPublisher
    {
        private readonly AzureEventGridSettings _settings;
        private readonly string _topic;
        private readonly EventGridPublisherClient _client;

        public EventGridPublisher(AzureEventGridSettings settings, string topic, EventGridPublisherClient client)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task PublishCloudEventsAsync(IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.SendEventsAsync(events, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to publish events to Event Grid topic '{_topic}'. Ensure the topic endpoint and access key are correctly configured.", ex);
            }
        }
    }
}