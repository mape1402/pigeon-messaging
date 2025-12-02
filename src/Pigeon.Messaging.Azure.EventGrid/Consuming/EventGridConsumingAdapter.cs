namespace Pigeon.Messaging.Azure.EventGrid.Consuming
{
    using global::Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using System.Collections.Concurrent;

    /// <summary>
    /// Consuming adapter for receiving messages from Azure Event Grid using webhooks and subscriptions.
    /// Manages event subscriptions for each topic and raises events when messages are consumed.
    /// </summary>
    internal class EventGridConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IEventGridProvider _eventGridProvider;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<EventGridConsumingAdapter> _logger;

        private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new();

        private readonly EventHandler<TopicEventArgs> _onTopicCreated;
        private readonly EventHandler<TopicEventArgs> _onTopicRemoved;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventGridConsumingAdapter"/> class.
        /// </summary>
        /// <param name="consumingConfigurator">The configurator for retrieving topics to consume.</param>
        /// <param name="eventGridProvider">The provider for Azure Event Grid clients and subscriptions.</param>
        /// <param name="globalSettings">Global messaging settings for domain and configuration.</param>
        /// <param name="logger">Logger for error and informational messages.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public EventGridConsumingAdapter(IConsumingConfigurator consumingConfigurator, IEventGridProvider eventGridProvider,
            IOptions<GlobalSettings> globalSettings, ILogger<EventGridConsumingAdapter> logger)
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _eventGridProvider = eventGridProvider ?? throw new ArgumentNullException(nameof(eventGridProvider));
            _globalSettings = globalSettings?.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _onTopicCreated = async (s, e) => await StartNewProcessor(e.Topic, CancellationToken.None);
            _onTopicRemoved = async (s, e) => await StopProcessor(e.Topic, CancellationToken.None);
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

            var topics = _consumingConfigurator.GetAllTopics();

            foreach (var topic in topics)
                await StartNewProcessor(topic, cancellationToken);

            _logger.LogInformation("AzureEventGridConsumingAdapter has been initialized");
        }

        /// <summary>
        /// Stops consuming messages by cancelling subscriptions and disposing all resources.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            _consumingConfigurator.TopicCreated -= _onTopicCreated;
            _consumingConfigurator.TopicRemoved -= _onTopicRemoved;

            foreach (var topic in _processors.Keys)
                await StopProcessor(topic, cancellationToken);

            _processors.Clear();

            _logger.LogInformation("AzureEventGridConsumingAdapter has been stopped gracefully");
        }

        private async Task StartNewProcessor(string topic, CancellationToken cancellationToken = default)
        {
            var processor = _eventGridProvider.CreateProcessor(topic);

            if (!_processors.TryAdd(topic, processor))
            {
                await processor.DisposeAsync();
                _logger.LogWarning("AzureServiceBusConsumingAdapter: Processor for topic '{Topic}' already exists. Skipping creation.", topic);
                return;
            }

            processor.ProcessMessageAsync += async args =>
            {
                try
                {
                    var body = args.Message.Body.ToArray();
                    var json = body.FromBytes();

                    MessageConsumed?.Invoke(this, new MessageConsumedEventArgs(topic, json));

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

        private async Task StopProcessor(string topic, CancellationToken cancellationToken = default)
        {
            if (!_processors.TryRemove(topic, out var processor))
                return;

            try
            {
                await processor.StopProcessingAsync(cancellationToken);
                await processor.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AzureServiceBusConsumingAdapter: Error while stopping processor for topic '{Topic}'", topic);
            }
        }
    }
}
