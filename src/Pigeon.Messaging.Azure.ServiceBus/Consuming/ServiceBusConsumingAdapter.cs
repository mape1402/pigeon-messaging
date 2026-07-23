namespace Pigeon.Messaging.Azure.ServiceBus.Consuming
{
    using global::Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Topology;
    using System.Collections.Concurrent;

    /// <summary>
    /// Consuming adapter for receiving messages from Azure Service Bus topics using a resolved IServiceBusProvider.
    /// Manages processors for each topic and raises events when messages are consumed.
    /// </summary>
    internal class ServiceBusConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IServiceBusProvider _serviceBusProvider;
        private readonly ITopologyProvisioningService _topologyProvisioningService;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<ServiceBusConsumingAdapter> _logger;

        private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new();

        private readonly EventHandler<TopicEventArgs> _onTopicCreated;
        private readonly EventHandler<TopicEventArgs> _onTopicRemoved;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBusConsumingAdapter"/> class.
        /// </summary>
        /// <param name="consumingConfigurator">The configurator for retrieving topics to consume.</param>
        /// <param name="serviceBusProvider">The provider for Azure Service Bus clients and senders.</param>
        /// <param name="globalSettings">Global messaging settings for domain and configuration.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public ServiceBusConsumingAdapter(IConsumingConfigurator consumingConfigurator, IServiceBusProvider serviceBusProvider,
            IOptions<GlobalSettings> globalSettings, ILogger<ServiceBusConsumingAdapter> logger)
            : this(consumingConfigurator, serviceBusProvider, NoopTopologyProvisioningService.Instance, globalSettings, logger)
        {
        }

        public ServiceBusConsumingAdapter(IConsumingConfigurator consumingConfigurator, IServiceBusProvider serviceBusProvider,
            ITopologyProvisioningService topologyProvisioningService, IOptions<GlobalSettings> globalSettings, ILogger<ServiceBusConsumingAdapter> logger)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _serviceBusProvider = serviceBusProvider ?? throw new ArgumentNullException(nameof(serviceBusProvider));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
            _globalSettings = globalSettings.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _onTopicCreated = async (s, e) => await StartNewProcessor(e.Endpoint, CancellationToken.None);
            _onTopicRemoved = async (s, e) => await StopProcessor(e.Endpoint, CancellationToken.None);
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

            foreach (var endpoint in endpoints)
                await StartNewProcessor(endpoint, cancellationToken);

            _logger.LogInformation("AzureServiceBusConsumingAdapter has been initialized");
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

            foreach (var endpoint in _processors.Keys.Select(ParseEndpointKey))
                await StopProcessor(endpoint, cancellationToken);

            _processors.Clear();

            _logger.LogInformation("AzureServiceBusConsumingAdapter has been stopped gracefully");
        }

        private async Task StartNewProcessor(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            await _topologyProvisioningService.EnsureConsumeTopologyAsync(endpoint, cancellationToken);

            var processor = endpoint.Subscription == ConsumerEndpoint.DefaultSubscription
                ? _serviceBusProvider.CreateProcessor(endpoint.Topic)
                : _serviceBusProvider.CreateProcessor(endpoint.Topic, endpoint.Subscription);

            if (!_processors.TryAdd(endpoint.Key, processor))
            {
                await processor.DisposeAsync();
                _logger.LogWarning("AzureServiceBusConsumingAdapter: Processor for topic '{Topic}' and subscription '{Subscription}' already exists. Skipping creation.", endpoint.Topic, endpoint.Subscription);
                return;
            }

            processor.ProcessMessageAsync += async args =>
            {
                try
                {
                    var body = args.Message.Body.ToArray();
                    var json = body.FromBytes();

                    MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(endpoint.Topic, json, endpoint.Subscription));

                    await args.CompleteMessageAsync(args.Message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AzureServiceBusConsumingAdapter: Has ocurred an unexpected error while consuming a message.");
                }
            };

            processor.ProcessErrorAsync += args =>
            {
                _logger.LogError(args.Exception, "AzureServiceBusConsumingAdapter: An error occurred while processing messages.");
                return Task.CompletedTask;
            };

            await processor.StartProcessingAsync(cancellationToken);
        }

        private async Task StopProcessor(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!_processors.TryRemove(endpoint.Key, out var processor))
                return;

            try
            {
                await processor.StopProcessingAsync(cancellationToken);
                await processor.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AzureServiceBusConsumingAdapter: Error while stopping processor for topic '{Topic}' and subscription '{Subscription}'", endpoint.Topic, endpoint.Subscription);
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
