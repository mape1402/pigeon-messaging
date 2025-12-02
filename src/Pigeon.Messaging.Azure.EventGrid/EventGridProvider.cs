namespace Pigeon.Messaging.Azure.EventGrid
{
    using global::Azure;
    using global::Azure.Messaging.EventGrid;
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
        /// Creates an Event Grid subscription for the specified topic.
        /// </summary>
        /// <param name="topic">The name of the topic.</param>
        /// <returns>An Event Grid subscription.</returns>
        IEventGridSubscription CreateSubscription(string topic);
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
    /// Defines a contract for subscribing to Event Grid events.
    /// </summary>
    public interface IEventGridSubscription : IAsyncDisposable
    {
        /// <summary>
        /// Occurs when a cloud event is received.
        /// </summary>
        event EventHandler<CloudEventReceivedEventArgs> CloudEventReceived;

        /// <summary>
        /// Starts the subscription to receive events.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the subscription from receiving events.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task StopAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Event arguments for when a cloud event is received.
    /// </summary>
    public class CloudEventReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CloudEventReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="eventGridEvent">The received Event Grid event.</param>
        public CloudEventReceivedEventArgs(EventGridEvent eventGridEvent)
        {
            CloudEvent = eventGridEvent ?? throw new ArgumentNullException(nameof(eventGridEvent));
        }

        /// <summary>
        /// Gets the received Event Grid event.
        /// </summary>
        public EventGridEvent CloudEvent { get; }
    }

    /// <summary>
    /// Provides methods to interact with Azure Event Grid, allowing the creation of clients and subscriptions for messaging operations.
    /// </summary>
    internal class EventGridProvider : IEventGridProvider
    {
        private readonly AzureEventGridSettings _settings;
        private readonly ConcurrentDictionary<string, IEventGridPublisher> _publisherClients = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridProvider"/> class.
        /// </summary>
        /// <param name="options">The Azure Event Grid settings options.</param>
        public EventGridProvider(IOptions<AzureEventGridSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public IEventGridPublisher GetClient(string topic)
        {
            return _publisherClients.GetOrAdd(topic, _ =>
            {
                var client = new EventGridPublisherClient(new Uri(_settings.TopicEndpoint), new AzureKeyCredential(_settings.AccessKey));
                return new EventGridPublisher(_settings, topic, client);
            });
        }

        /// <inheritdoc />
        public IEventGridSubscription CreateSubscription(string topic)
        {
            return new EventGridSubscription(_settings, topic);
        }
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

    /// <summary>
    /// Event Grid subscription implementation.
    /// </summary>
    internal class EventGridSubscription : IEventGridSubscription
    {
        private readonly AzureEventGridSettings _settings;
        private readonly string _topic;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task _webhookListenerTask;

        public EventGridSubscription(AzureEventGridSettings settings, string topic)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _topic = topic ?? throw new ArgumentNullException(nameof(topic));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public event EventHandler<CloudEventReceivedEventArgs> CloudEventReceived;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.WebhookEndpoint))
            {
                throw new InvalidOperationException("WebhookEndpoint must be configured to start Event Grid subscription. Event Grid requires a webhook endpoint to receive events.");
            }

            // Start webhook listener task
            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;
            _webhookListenerTask = Task.Run(() => SimulateWebhookListener(linkedToken), linkedToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource?.Cancel();
            return _webhookListenerTask ?? Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task SimulateWebhookListener(CancellationToken cancellationToken)
        {
            // This is a simulation of a webhook listener. In a real implementation, 
            // you would set up an HTTP server to listen for Event Grid webhook calls
            // or use Azure Event Grid SDK for webhook validation and event processing.
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Simulate webhook receiving events - in real scenarios this would be
                    // triggered by actual HTTP requests from Event Grid
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    
                    // Note: Real implementation would parse incoming HTTP requests,
                    // validate Event Grid signatures, and extract EventGridEvent objects
                    // from the request body, then raise CloudEventReceived events.
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception)
                {
                    // Log errors in real implementation
                    break;
                }
            }
        }
    }
}