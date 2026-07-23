namespace Pigeon.Messaging.Azure.EventHub.Consuming
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Topology;
    using System.Collections.Concurrent;

    /// <summary>
    /// Consuming adapter for receiving messages from Azure Event Hubs using event processors.
    /// Manages processors for each topic and raises events when messages are consumed.
    /// </summary>
    internal class EventHubConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IEventHubProvider _eventHubProvider;
        private readonly ITopologyProvisioningService _topologyProvisioningService;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<EventHubConsumingAdapter> _logger;

        private readonly ConcurrentDictionary<string, IEventHubProcessor> _processors = new();
        private readonly ConcurrentDictionary<string, Task> _listeners = new();
        private readonly EventHandler<TopicEventArgs> _onTopicCreated;
        private readonly EventHandler<TopicEventArgs> _onTopicRemoved;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHubConsumingAdapter"/> class.
        /// </summary>
        /// <param name="consumingConfigurator">The configurator for retrieving topics to consume.</param>
        /// <param name="eventHubProvider">The provider for Azure Event Hub clients and processors.</param>
        /// <param name="globalSettings">Global messaging settings for domain and configuration.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public EventHubConsumingAdapter(IConsumingConfigurator consumingConfigurator, IEventHubProvider eventHubProvider,
            IOptions<GlobalSettings> globalSettings, ILogger<EventHubConsumingAdapter> logger)
            : this(consumingConfigurator, eventHubProvider, NoopTopologyProvisioningService.Instance, globalSettings, logger)
        {
        }

        public EventHubConsumingAdapter(IConsumingConfigurator consumingConfigurator, IEventHubProvider eventHubProvider,
            ITopologyProvisioningService topologyProvisioningService, IOptions<GlobalSettings> globalSettings, ILogger<EventHubConsumingAdapter> logger)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _eventHubProvider = eventHubProvider ?? throw new ArgumentNullException(nameof(eventHubProvider));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
            _globalSettings = globalSettings?.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _onTopicCreated = async (s, e) => await StartNewProcessor(e.Endpoint, CancellationToken.None);
            _onTopicRemoved = (s, e) => StopProcessor(e.Endpoint);
        }

        /// <summary>
        /// Event raised when a message is consumed from any of the configured topics.
        /// </summary>
        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        /// <summary>
        /// Starts consuming messages asynchronously from all configured topics.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous start operation.</returns>
        public async ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated += _onTopicCreated;
            _consumingConfigurator.TopicRemoved += _onTopicRemoved;

            var endpoints = GetConfiguredEndpoints();
            var cts = new CancellationTokenSource();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            foreach (var endpoint in endpoints)
                await StartNewProcessor(endpoint, cancellationToken);

            _logger.LogInformation("AzureEventHubConsumingAdapter has been initialized");
        }

        /// <summary>
        /// Stops consuming messages by cancelling processors and disposing all resources.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated -= _onTopicCreated;
            _consumingConfigurator.TopicRemoved -= _onTopicRemoved;

            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            try
            {
                await Task.WhenAll(_listeners.Values);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AzureEventHubConsumingAdapter: Unexpected error while waiting for Event Hub listeners to stop.");
            }

            foreach (var endpoint in _processors.Keys.Select(ParseEndpointKey))
                StopProcessor(endpoint);

            _processors.Clear();
            _listeners.Clear();

            _logger.LogInformation("AzureEventHubConsumingAdapter has been stopped gracefully");
        }

        private async Task StartNewProcessor(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            try
            {
                await _topologyProvisioningService.EnsureConsumeTopologyAsync(endpoint, cancellationToken);

                var processor = endpoint.Subscription == ConsumerEndpoint.DefaultSubscription
                    ? _eventHubProvider.CreateProcessor(endpoint.Topic)
                    : _eventHubProvider.CreateProcessor(endpoint.Topic, endpoint.Subscription);

                if (!_processors.TryAdd(endpoint.Key, processor))
                {
                    processor.Dispose();
                    _logger.LogWarning("AzureEventHubConsumingAdapter: Processor for topic '{Topic}' and subscription '{Subscription}' already exists. Skipping creation.", endpoint.Topic, endpoint.Subscription);
                    return;
                }

                var listener = Task.Run(() => ListenToEvents(processor, endpoint, _cancellationTokenSource.Token));
                _listeners.TryAdd(endpoint.Key, listener);

                _logger.LogInformation("AzureEventHubConsumingAdapter: Started processor for topic '{Topic}' and subscription '{Subscription}'.", endpoint.Topic, endpoint.Subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AzureEventHubConsumingAdapter: Error starting processor for topic '{Topic}' and subscription '{Subscription}'.", endpoint.Topic, endpoint.Subscription);
            }
        }

        private void StopProcessor(ConsumerEndpoint endpoint)
        {
            if (!_processors.TryRemove(endpoint.Key, out var processor))
                return;

            try
            {
                processor.Dispose();
                _listeners[endpoint.Key] = default;

                _logger.LogInformation("AzureEventHubConsumingAdapter: Stopped processor for topic '{Topic}' and subscription '{Subscription}'.", endpoint.Topic, endpoint.Subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AzureEventHubConsumingAdapter: Error while stopping processor for topic '{Topic}' and subscription '{Subscription}'", endpoint.Topic, endpoint.Subscription);
            }
        }

        private async Task ListenToEvents(IEventHubProcessor processor, ConsumerEndpoint endpoint, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var partitionEvent in processor.ReadEventsAsync(cancellationToken))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        var eventData = partitionEvent.Data;
                        var json = eventData.EventBody.ToArray().FromBytes();

                        MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(endpoint.Topic, json, endpoint.Subscription));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "AzureEventHubConsumingAdapter: Has occurred an unexpected error while processing event from topic '{Topic}' and subscription '{Subscription}'.", endpoint.Topic, endpoint.Subscription);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AzureEventHubConsumingAdapter: Error in event listening loop for topic '{Topic}' and subscription '{Subscription}'.", endpoint.Topic, endpoint.Subscription);
            }
        }

        private static ConsumerEndpoint ParseEndpointKey(string key)
        {
            var parts = key.Split("::", 2, StringSplitOptions.None);
            return new ConsumerEndpoint(parts[0], parts.Length > 1 ? parts[1] : ConsumerEndpoint.DefaultSubscription);
        }

        private IEnumerable<ConsumerEndpoint> GetConfiguredEndpoints()
        {
            var endpoints = _consumingConfigurator.GetAllEndpoints()?.ToArray();

            if (endpoints is { Length: > 0 })
                return endpoints;

            return _consumingConfigurator.GetAllTopics()?.Select(topic => new ConsumerEndpoint(topic)) ?? Enumerable.Empty<ConsumerEndpoint>();
        }
    }
}
