namespace Pigeon.Messaging.Consuming.Management
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Topology;
    using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<Guid, Task> _inFlightDispatches = new();
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
            _messageQueue = CreateMessageQueue();

            var maxConcurrency = GetMaxConcurrency();
            _workers = maxConcurrency.HasValue
                ? Enumerable.Range(0, maxConcurrency.Value)
                    .Select(_ => Task.Run(() => ProcessMessagesAsync(_workerCancellationTokenSource.Token), CancellationToken.None))
                    .ToArray()
                : new[] { Task.Run(() => ProcessMessagesWithoutConcurrencyLimitAsync(_workerCancellationTokenSource.Token), CancellationToken.None) };

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

            if (!_inFlightDispatches.IsEmpty)
                await Task.WhenAll(_inFlightDispatches.Values);

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

        private async Task ProcessMessagesWithoutConcurrencyLimitAsync(CancellationToken cancellationToken)
        {
            await foreach (var message in _messageQueue.Reader.ReadAllAsync(cancellationToken))
                TrackDispatch(message, cancellationToken);
        }

        private void TrackDispatch(MessageConsumedEventArgs message, CancellationToken cancellationToken)
        {
            var id = Guid.NewGuid();
            var dispatch = Task.Run(() => DispatchMessageAsync(message, cancellationToken), CancellationToken.None);

            _inFlightDispatches.TryAdd(id, dispatch);
            _ = dispatch.ContinueWith(
                _ => _inFlightDispatches.TryRemove(id, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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

        private Channel<MessageConsumedEventArgs> CreateMessageQueue()
        {
            var queueCapacity = GetQueueCapacity();

            if (queueCapacity.HasValue)
            {
                return Channel.CreateBounded<MessageConsumedEventArgs>(new BoundedChannelOptions(queueCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                });
            }

            return Channel.CreateUnbounded<MessageConsumedEventArgs>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });
        }

        private int? GetMaxConcurrency()
        {
            var maxConcurrency = _globalSettings.ConsumerExecution?.MaxConcurrency;

            return maxConcurrency > 0 ? maxConcurrency.Value : null;
        }

        private int? GetQueueCapacity()
        {
            var queueCapacity = _globalSettings.ConsumerExecution?.QueueCapacity;

            return queueCapacity > 0 ? queueCapacity.Value : null;
        }

        private TimeSpan GetHandlerTimeout()
            => _globalSettings.ConsumerExecution?.HandlerTimeout > TimeSpan.Zero
                ? _globalSettings.ConsumerExecution.HandlerTimeout
                : TimeSpan.FromSeconds(30);

        private MessageAcknowledgementMode GetAcknowledgementMode()
            => _globalSettings.ConsumerExecution?.AcknowledgementMode ?? MessageAcknowledgementMode.Manual;
    }
}
