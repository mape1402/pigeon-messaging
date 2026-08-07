namespace Pigeon.Testing
{
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;

    internal sealed class PigeonTestingProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly PigeonTestingTransport _transport;

        public PigeonTestingProducingAdapter(PigeonTestingTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
            => PublishMessageAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

        public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
            => _transport.CaptureWrappedAsync(payload, route, cancellationToken);

        public ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PublishRawMessageAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

        public ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
            => _transport.CaptureRawAsync(message, route, cancellationToken);
    }
}
