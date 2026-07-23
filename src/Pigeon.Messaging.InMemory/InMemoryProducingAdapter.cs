namespace Pigeon.Messaging.InMemory
{
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;

    internal sealed class InMemoryProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly InMemoryMessageBroker _broker;
        private readonly ISerializer _serializer;

        public InMemoryProducingAdapter(InMemoryMessageBroker broker, ISerializer serializer)
        {
            _broker = broker ?? throw new ArgumentNullException(nameof(broker));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
            where T : class
            => PublishMessageAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

        public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
            where T : class
            => PublishCoreAsync(payload, route, false, cancellationToken);

        public ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
            where T : class
            => PublishRawMessageAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

        public ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
            where T : class
            => PublishCoreAsync(message, route, true, cancellationToken);

        private ValueTask PublishCoreAsync(object payload, PublishingRoute route, bool isRaw, CancellationToken cancellationToken)
            => _broker.PublishAsync(_serializer.Serialize(payload), route, isRaw, cancellationToken);
    }
}
