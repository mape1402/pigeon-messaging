namespace Pigeon.Messaging.InMemory
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;

    internal sealed class InMemoryConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        private readonly InMemoryMessageBroker _broker;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private int _started;

        public InMemoryConsumingAdapter(InMemoryMessageBroker broker, IConsumingConfigurator consumingConfigurator)
        {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
        }

        public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

        public ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                return ValueTask.CompletedTask;

            _broker.MessagePublished += OnMessagePublishedAsync;
            return ValueTask.CompletedTask;
        }

        public ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _started, 0) == 0)
                return ValueTask.CompletedTask;

            _broker.MessagePublished -= OnMessagePublishedAsync;
            return ValueTask.CompletedTask;
        }

        private ValueTask OnMessagePublishedAsync(InMemoryPublishedMessage message, CancellationToken cancellationToken)
        {
            if (message.IsRaw)
                return ValueTask.CompletedTask;

            var endpoints = _consumingConfigurator.GetAllEndpoints()
                .Where(endpoint => string.Equals(endpoint.Topic, message.Route.Topic, StringComparison.Ordinal))
                .ToArray();

            foreach (var endpoint in endpoints)
            {
                var delivery = _broker.CreateDelivery(message, endpoint);
                MessageConsumed?.Invoke(
                    this,
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
                        }));
            }

            return ValueTask.CompletedTask;
        }
    }
}
