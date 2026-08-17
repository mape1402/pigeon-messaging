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
        private readonly ConsumerExecutionDiagnostics _diagnostics;
        private readonly ILogger<ConsumingManager> _logger;

        private CancellationToken _backgroundCancellationToken;
        private Channel<QueuedConsumedMessage> _messageQueue;
        private readonly ConcurrentDictionary<Guid, Task> _inFlightDispatches = new();
        private CancellationTokenSource _workerCancellationTokenSource;
        private Task[] _workers = Array.Empty<Task>();

        public ConsumingManager(
            IConsumingDispatcher dispatcher,
            IEnumerable<IMessageBrokerConsumingAdapter> messageBrokerAdapters,
            IOptions<GlobalSettings> globalSettings,
            ILogger<ConsumingManager> logger)
            : this(dispatcher, messageBrokerAdapters, new Configuration.ConsumingConfigurator(), NoopTopologyProvisioningService.Instance, globalSettings, new ConsumerExecutionDiagnostics(), logger)
        {
        }

        public ConsumingManager(
            IConsumingDispatcher dispatcher,
            IEnumerable<IMessageBrokerConsumingAdapter> messageBrokerAdapters,
            IConsumingConfigurator consumingConfigurator,
            ITopologyProvisioningService topologyProvisioningService,
            IOptions<GlobalSettings> globalSettings,
            ILogger<ConsumingManager> logger)
            : this(dispatcher, messageBrokerAdapters, consumingConfigurator, topologyProvisioningService, globalSettings, new ConsumerExecutionDiagnostics(), logger)
        {
        }

        public ConsumingManager(
            IConsumingDispatcher dispatcher,
            IEnumerable<IMessageBrokerConsumingAdapter> messageBrokerAdapters,
            IConsumingConfigurator consumingConfigurator,
            ITopologyProvisioningService topologyProvisioningService,
            IOptions<GlobalSettings> globalSettings,
            ConsumerExecutionDiagnostics diagnostics,
            ILogger<ConsumingManager> logger)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBrokerAdapters = messageBrokerAdapters ?? throw new ArgumentNullException(nameof(messageBrokerAdapters));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
            _globalSettings = globalSettings?.Value ?? throw new ArgumentNullException(nameof(globalSettings));
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _backgroundCancellationToken = cancellationToken;
            _workerCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _messageQueue = CreateMessageQueue();
            _diagnostics.Configure(_globalSettings.ConsumerExecution);

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
                adapter.MessageConsumedAsync += MessageConsumedAsync;
                await adapter.StartConsumeAsync(cancellationToken);
            } 
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            foreach (var adapter in _messageBrokerAdapters)
            {
                adapter.MessageConsumed -= MessageConsumed;
                adapter.MessageConsumedAsync -= MessageConsumedAsync;
            }

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

        private async ValueTask MessageConsumedAsync(object sender, MessageConsumedEventArgs e, CancellationToken cancellationToken = default)
        {
            try
            {
                _diagnostics.RecordReceived();
                _diagnostics.RecordQueued();
                await _messageQueue.Writer.WriteAsync(
                    new QueuedConsumedMessage(e, DateTimeOffset.UtcNow),
                    cancellationToken.CanBeCanceled ? cancellationToken : _backgroundCancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Has occurred an unexpected error when a message was enqueued for dispatch.");
                _diagnostics.RecordQueueWriteFailed();
                _diagnostics.RecordRejected();
                await e.FailAsync(ex, cancellationToken.CanBeCanceled ? cancellationToken : _backgroundCancellationToken);
            }
        }

        private void MessageConsumed(object sender, MessageConsumedEventArgs e)
            => _ = MessageConsumedAsync(sender, e, _backgroundCancellationToken).AsTask();

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

        private void TrackDispatch(QueuedConsumedMessage message, CancellationToken cancellationToken)
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

        private async Task DispatchMessageAsync(QueuedConsumedMessage queuedMessage, CancellationToken cancellationToken)
        {
            var e = queuedMessage.Message;
            _diagnostics.RecordDequeued(DateTimeOffset.UtcNow - queuedMessage.EnqueuedOnUtc);
            _diagnostics.RecordHandlerStarted();

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
                    token => CompleteMessageAsync(e, token),
                    (ex, token) => FailMessageAsync(e, ex, token),
                    linkedCts.Token);

                if (GetAcknowledgementMode() == MessageAcknowledgementMode.OnHandlerSuccess)
                    await CompleteMessageAsync(e, cancellationToken);

                _diagnostics.RecordHandlerCompleted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Has occurred an unexpected error when a message has been consumed.");

                if (GetAcknowledgementMode() == MessageAcknowledgementMode.OnHandlerSuccess)
                    await FailMessageAsync(e, ex, cancellationToken);

                _diagnostics.RecordHandlerFailed();
            }
        }

        private async Task CompleteMessageAsync(MessageConsumedEventArgs e, CancellationToken cancellationToken)
        {
            await e.CompleteAsync(cancellationToken);
            _diagnostics.RecordAcknowledged();
        }

        private async Task FailMessageAsync(MessageConsumedEventArgs e, Exception exception, CancellationToken cancellationToken)
        {
            await e.FailAsync(exception, cancellationToken);
            _diagnostics.RecordRejected();
        }

        private Channel<QueuedConsumedMessage> CreateMessageQueue()
        {
            var queueCapacity = GetQueueCapacity();

            if (queueCapacity.HasValue)
            {
                return Channel.CreateBounded<QueuedConsumedMessage>(new BoundedChannelOptions(queueCapacity.Value)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                });
            }

            return Channel.CreateUnbounded<QueuedConsumedMessage>(new UnboundedChannelOptions
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
