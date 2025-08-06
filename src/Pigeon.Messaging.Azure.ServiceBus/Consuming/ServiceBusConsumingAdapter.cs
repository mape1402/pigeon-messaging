namespace Pigeon.Messaging.Azure.ServiceBus.Consuming
{
    using global::Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using System.Collections.Concurrent;
    using System.Text;

    /// <summary>
    /// Consuming adapter for receiving messages from Azure Service Bus topics using a resolved IServiceBusProvider.
    /// Manages processors for each topic and raises events when messages are consumed.
    /// </summary>
    internal class ServiceBusConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IServiceBusProvider _serviceBusProvider;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<ServiceBusConsumingAdapter> _logger;

        private readonly ConcurrentDictionary<string, ServiceBusProcessor> _processors = new();

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
        {
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _serviceBusProvider = serviceBusProvider ?? throw new ArgumentNullException(nameof(serviceBusProvider));
            _globalSettings = globalSettings.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            var topics = _consumingConfigurator.GetAllTopics();

            foreach (var topic in topics)
            {
                var processor = _serviceBusProvider.CreateProcessor($"{_globalSettings.Domain}.{topic}");

                if(!_processors.TryAdd(topic, processor))
                {
                    await processor.DisposeAsync();
                    _logger.LogWarning("AzureServiceBusConsumingAdapter: Processor for topic '{Topic}' already exists. Skipping creation.", topic);
                    continue;
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

            _logger.LogInformation("AzureServiceBusConsumingAdapter has been initialized");
        }

        /// <summary>
        /// Stops consuming messages by cancelling processors and disposing all resources.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous stop operation.</returns>
        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            foreach(var processor in _processors.Values)
            {
                try
                {
                    await processor.StopProcessingAsync(cancellationToken);
                    await processor.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AzureServiceBusConsumingAdapter: Error while stopping processor for topic.");
                }
            }

            _processors.Clear();

            _logger.LogInformation("AzureServiceBusConsumingAdapter has been stopped gracefully");
        }
    }
}
