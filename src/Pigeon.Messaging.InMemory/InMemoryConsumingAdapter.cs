namespace Pigeon.Messaging.InMemory
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using System.Collections.Concurrent;

    internal sealed class InMemoryConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly InMemoryMessageBroker _broker;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly InMemoryBrokerSettings _settings;
        private readonly ILogger<InMemoryConsumingAdapter> _logger;
        private readonly ConcurrentDictionary<long, Task> _retryTasks = new();
        private readonly object _retrySync = new();
        private CancellationTokenSource _retryCancellation;
        private long _nextRetryId;
        private int _started;

        public InMemoryConsumingAdapter(InMemoryMessageBroker broker, IConsumingConfigurator consumingConfigurator,
            InMemoryBrokerSettings settings = null, ILogger<InMemoryConsumingAdapter> logger = null)
        {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _settings = settings ?? new InMemoryBrokerSettings();
            _logger = logger ?? NullLogger<InMemoryConsumingAdapter>.Instance;
        }

        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        public event MessageConsumedAsyncHandler MessageConsumedAsync;

        public ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            lock (_retrySync)
            {
                if (_started == 1)
                    return ValueTask.CompletedTask;
                _retryCancellation = new CancellationTokenSource();
                _started = 1;
                _broker.MessagePublished += OnMessagePublishedAsync;
            }
            return ValueTask.CompletedTask;
        }

        public async ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            CancellationTokenSource cancellation;
            Task[] pending;
            lock (_retrySync)
            {
                if (_started == 0)
                    return;
                _started = 0;
                _broker.MessagePublished -= OnMessagePublishedAsync;
                cancellation = _retryCancellation;
                cancellation.Cancel();
                pending = _retryTasks.Values.ToArray();
            }
            await Task.WhenAll(pending);
            cancellation.Dispose();
        }

        private async ValueTask OnMessagePublishedAsync(InMemoryPublishedMessage message, CancellationToken cancellationToken)
        {
            if (message.IsRaw)
                return;

            var endpoints = _consumingConfigurator.GetAllEndpoints()
                .Where(endpoint => string.Equals(endpoint.Topic, message.Route.Topic, StringComparison.Ordinal))
                .ToArray();

            foreach (var endpoint in endpoints)
                await DeliverAsync(message, endpoint, 1, cancellationToken);
        }

        private async ValueTask DeliverAsync(InMemoryPublishedMessage message, ConsumerEndpoint endpoint, int attempt, CancellationToken cancellationToken)
        {
            var delivery = _broker.CreateDelivery(message, endpoint, attempt);
            await OnMessageConsumedAsync(
                    new MessageConsumedEventArgs(
                        endpoint.Topic,
                        message.Payload,
                        endpoint.Subscription,
                        _ =>
                        {
                            delivery.Completed = true;
                            return Task.CompletedTask;
                        },
                        (exception, _) =>
                        {
                            delivery.Failed = true;
                            delivery.Error = exception?.Message;
                            return Task.CompletedTask;
                        },
                        (exception, _) =>
                        {
                            delivery.Failed = true;
                            delivery.Error = exception?.Message;
                            ScheduleRetry(message, endpoint, attempt + 1);
                            return Task.CompletedTask;
                        },
                        (exception, _) =>
                        {
                            delivery.Failed = true;
                            delivery.Error = exception?.Message;
                            return Task.CompletedTask;
                        }),
                    cancellationToken);
        }

        private void ScheduleRetry(InMemoryPublishedMessage message, ConsumerEndpoint endpoint, int attempt)
        {
            lock (_retrySync)
            {
                if (_started == 0)
                    throw new InvalidOperationException("The in-memory consumer is not running.");
                var id = ++_nextRetryId;
                var task = RetryAsync(message, endpoint, attempt, _retryCancellation.Token);
                _retryTasks[id] = task;
                _ = task.ContinueWith(_ => _retryTasks.TryRemove(id, out _), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        private async Task RetryAsync(InMemoryPublishedMessage message, ConsumerEndpoint endpoint, int attempt, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(_settings.RetryDelay, cancellationToken);
                await DeliverAsync(message, endpoint, attempt, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                _logger.LogError(exception, "In-memory redelivery failed for topic {Topic}, subscription {Subscription}, message {MessageId}, attempt {Attempt}",
                    endpoint.Topic, endpoint.Subscription, message.Id, attempt);
            }
        }

        private async ValueTask OnMessageConsumedAsync(MessageConsumedEventArgs args, CancellationToken cancellationToken)
        {
            var asyncHandler = MessageConsumedAsync;
            if (asyncHandler == null)
            {
                var syncHandler = MessageConsumed;
                if (syncHandler != null)
                    _ = Task.Run(() => syncHandler(this, args), CancellationToken.None);

                return;
            }

            foreach (MessageConsumedAsyncHandler handler in asyncHandler.GetInvocationList())
                await handler(this, args, cancellationToken);
        }
    }
}
