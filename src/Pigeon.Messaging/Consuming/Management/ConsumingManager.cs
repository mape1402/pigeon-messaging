namespace Pigeon.Messaging.Consuming.Management
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Topology;
    using System.Threading.Channels;

    internal class ConsumingManager : IConsumingManager
    {
        private readonly IConsumingDispatcher _dispatcher;
        private readonly IEnumerable<IMessageBrokerConsumingAdapter> _messageBrokerAdapters;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly ITopologyProvisioningService _topologyProvisioningService;
        private readonly GlobalSettings _globalSettings;
        private readonly ILogger<ConsumingManager> _logger;

        private CancellationToken _backgroundCancellationToken;
        private Channel<MessageConsumedEventArgs> _messageQueue;
        private CancellationTokenSource _workerCancellationTokenSource;
        private Task[] _workers = Array.Empty<Task>();

        public ConsumingManager(
            IConsumingDispatcher dispatcher,
            IEnumerable<IMessageBrokerConsumingAdapter> messageBrokerAdapters,
            IOptions<GlobalSettings> globalSettings,
            ILogger<ConsumingManager> logger)
            : this(dispatcher, messageBrokerAdapters, new Configuration.ConsumingConfigurator(), NoopTopologyProvisioningService.Instance, globalSettings, logger)
        {
        }

        public ConsumingManager(
            IConsumingDispatcher dispatcher,
            IEnumerable<IMessageBrokerConsumingAdapter> messageBrokerAdapters,
            IConsumingConfigurator consumingConfigurator,
            ITopologyProvisioningService topologyProvisioningService,
            IOptions<GlobalSettings> globalSettings,
            ILogger<ConsumingManager> logger)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBrokerAdapters = messageBrokerAdapters ?? throw new ArgumentNullException(nameof(messageBrokerAdapters));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
            _globalSettings = globalSettings?.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _backgroundCancellationToken = cancellationToken;
            _workerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _messageQueue = Channel.CreateBounded<MessageConsumedEventArgs>(new BoundedChannelOptions(GetQueueCapacity())
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
            _workers = Enumerable.Range(0, GetMaxConcurrency())
                .Select(_ => Task.Run(() => ProcessMessagesAsync(_workerCancellationTokenSource.Token), CancellationToken.None))
                .ToArray();

            foreach (var endpoint in _consumingConfigurator.GetAllEndpoints())
                await _topologyProvisioningService.EnsureConsumeTopologyAsync(endpoint, cancellationToken);

            foreach (var adapter in _messageBrokerAdapters)
            {
                adapter.MessageConsumed += MessageConsumed;
                await adapter.StartConsumeAsync(cancellationToken);
            } 
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            foreach (var adapter in _messageBrokerAdapters)
                adapter.MessageConsumed -= MessageConsumed;

            if (_messageQueue != null)
                _messageQueue.Writer.TryComplete();

            if (_workers.Length > 0)
                await Task.WhenAll(_workers);

            foreach (var adapter in _messageBrokerAdapters)
            {
                try
                {
                    await adapter.StopConsumeAsync(_backgroundCancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to stop adapter {adapter.GetType().Name} gracefully.");
                }
            }

            _workerCancellationTokenSource?.Dispose();
        }

        private void MessageConsumed(object sender, MessageConsumedEventArgs e)
        {
            try
            {
                _messageQueue.Writer.WriteAsync(e, _backgroundCancellationToken).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Has occurred an unexpected error when a message was enqueued for dispatch.");
                e.FailAsync(ex, _backgroundCancellationToken).GetAwaiter().GetResult();
            }
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _messageQueue.Reader.ReadAllAsync(cancellationToken))
                await DispatchMessageAsync(message, cancellationToken);
        }

        private async Task DispatchMessageAsync(MessageConsumedEventArgs e, CancellationToken cancellationToken)
        {
            try
            {
                using var timeoutCts = new CancellationTokenSource(GetHandlerTimeout());
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                var rawPayload = new RawPayload(e.RawPayload);

                var topic = e.Topic;

                if (!string.IsNullOrWhiteSpace(_globalSettings.Domain))
                    topic = topic.Replace($"{_globalSettings.Domain}.", string.Empty);

                var subscription = e.Subscription == Configuration.ConsumerEndpoint.DefaultSubscription
                    ? Configuration.ConsumerEndpoint.DefaultSubscription
                    : e.Subscription;

                await _dispatcher.DispatchAsync(
                    topic,
                    subscription,
                    rawPayload,
                    e.CompleteAsync,
                    e.FailAsync,
                    linkedCts.Token);

                if (GetAcknowledgementMode() == MessageAcknowledgementMode.OnHandlerSuccess)
                    await e.CompleteAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Has occurred an unexpected error when a message has been consumed.");

                if (GetAcknowledgementMode() == MessageAcknowledgementMode.OnHandlerSuccess)
                    await e.FailAsync(ex, cancellationToken);
            }
        }

        private int GetMaxConcurrency()
            => Math.Max(1, _globalSettings.ConsumerExecution?.MaxConcurrency ?? Environment.ProcessorCount);

        private int GetQueueCapacity()
            => Math.Max(1, _globalSettings.ConsumerExecution?.QueueCapacity ?? 1_000);

        private TimeSpan GetHandlerTimeout()
            => _globalSettings.ConsumerExecution?.HandlerTimeout > TimeSpan.Zero
                ? _globalSettings.ConsumerExecution.HandlerTimeout
                : TimeSpan.FromSeconds(30);

        private MessageAcknowledgementMode GetAcknowledgementMode()
            => _globalSettings.ConsumerExecution?.AcknowledgementMode ?? MessageAcknowledgementMode.Manual;
    }
}
