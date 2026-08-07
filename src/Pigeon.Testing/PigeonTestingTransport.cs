namespace Pigeon.Testing
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using System.Collections.Concurrent;

    internal sealed class PigeonTestingTransport : IPigeonTestingTransport
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ISerializer _serializer;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly IConsumingDispatcher _dispatcher;
        private readonly ConcurrentQueue<PigeonTestingMessage> _pending = new();
        private readonly ConcurrentQueue<PigeonTestingMessage> _published = new();
        private readonly ConcurrentQueue<PigeonTestingMessage> _consumed = new();
        private readonly ConcurrentQueue<PigeonTestingMessage> _deadLetters = new();
        private readonly ConcurrentQueue<PigeonTestingFailure> _failures = new();
        private readonly ConcurrentDictionary<Type, ConcurrentQueue<Exception>> _failNext = new();

        public PigeonTestingTransport(
            IServiceProvider serviceProvider,
            ISerializer serializer,
            IConsumingConfigurator consumingConfigurator,
            IConsumingDispatcher dispatcher)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public IReadOnlyCollection<object> Messages => _published.Select(message => message.Message).ToArray();

        public IReadOnlyCollection<PigeonTestingMessage> PublishedMessages => _published.ToArray();

        public IReadOnlyCollection<PigeonTestingMessage> ConsumedMessages => _consumed.ToArray();

        public IReadOnlyCollection<PigeonTestingMessage> DeadLetterMessages => _deadLetters.ToArray();

        public IReadOnlyCollection<PigeonTestingFailure> ConsumerFailures => _failures.ToArray();

        public ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
            => PublishAsync(message, typeof(T).Name, cancellationToken);

        public async ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
        {
            using var scope = _serviceProvider.CreateScope();
            var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
            await producer.PublishAsync(message, topic, cancellationToken);
        }

        public async Task DispatchPendingAsync(CancellationToken cancellationToken = default)
        {
            while (_pending.TryDequeue(out var pendingMessage))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (TryGetNextFailure(pendingMessage.MessageType, out var simulatedFailure))
                {
                    RecordFailure(pendingMessage, simulatedFailure, PigeonTestingFailureKind.Infrastructure, null);
                    continue;
                }

                if (pendingMessage.IsRaw)
                    continue;

                var endpoints = _consumingConfigurator
                    .GetAllEndpoints()
                    .Where(endpoint => string.Equals(endpoint.Topic, pendingMessage.Topic, StringComparison.Ordinal))
                    .ToArray();

                foreach (var endpoint in endpoints)
                    await DispatchToEndpointAsync(pendingMessage, endpoint, cancellationToken);
            }
        }

        public void FailNext<T>(Exception exception) where T : class
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            var queue = _failNext.GetOrAdd(typeof(T), _ => new ConcurrentQueue<Exception>());
            queue.Enqueue(exception);
        }

        public void Clear()
        {
            Drain(_pending);
            Drain(_published);
            Drain(_consumed);
            Drain(_deadLetters);
            Drain(_failures);
            _failNext.Clear();
        }

        internal ValueTask CaptureWrappedAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            var rawJson = _serializer.Serialize(payload);
            var topic = GetTopic(route);
            var message = new PigeonTestingMessage(
                payload.Message,
                topic,
                route,
                payload.Metadata,
                rawJson,
                false,
                payload.CreatedOnUtc);

            _published.Enqueue(message);
            _pending.Enqueue(message);

            return ValueTask.CompletedTask;
        }

        internal ValueTask CaptureRawAsync<T>(T rawMessage, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
        {
            if (rawMessage == null)
                throw new ArgumentNullException(nameof(rawMessage));

            var topic = GetTopic(route);
            var message = new PigeonTestingMessage(
                rawMessage,
                topic,
                route,
                new Dictionary<string, object>(),
                _serializer.Serialize(rawMessage),
                true,
                DateTimeOffset.UtcNow);

            _published.Enqueue(message);
            _pending.Enqueue(message);

            return ValueTask.CompletedTask;
        }

        private async Task DispatchToEndpointAsync(PigeonTestingMessage pendingMessage, ConsumerEndpoint endpoint, CancellationToken cancellationToken)
        {
            var dispatchedMessage = pendingMessage.CloneForDispatch();

            try
            {
                await _dispatcher.DispatchAsync(
                    pendingMessage.Topic,
                    endpoint.Subscription,
                    new RawPayload(pendingMessage.RawJson),
                    _ => Task.CompletedTask,
                    (_, _) => Task.CompletedTask,
                    cancellationToken);

                _consumed.Enqueue(dispatchedMessage.MarkConsumed(endpoint.Subscription));
            }
            catch (Exception exception)
            {
                RecordFailure(dispatchedMessage, exception, PigeonTestingFailureKind.Consumer, endpoint.Subscription);
            }
        }

        private void RecordFailure(PigeonTestingMessage message, Exception exception, PigeonTestingFailureKind kind, string subscription)
        {
            var failedMessage = message.MarkFailed(exception, subscription);
            _deadLetters.Enqueue(failedMessage);
            _failures.Enqueue(new PigeonTestingFailure(failedMessage, exception, kind));
        }

        private bool TryGetNextFailure(Type messageType, out Exception exception)
        {
            exception = null;

            if (messageType == null)
                return false;

            if (_failNext.TryGetValue(messageType, out var exactQueue) && exactQueue.TryDequeue(out exception))
                return true;

            foreach (var pair in _failNext)
            {
                if (pair.Key.IsAssignableFrom(messageType) && pair.Value.TryDequeue(out exception))
                    return true;
            }

            return false;
        }

        private static string GetTopic(PublishingRoute route)
            => route.Topic;

        private static void Drain<T>(ConcurrentQueue<T> queue)
        {
            while (queue.TryDequeue(out _))
            {
            }
        }
    }
}
