namespace Pigeon.Messaging.Producing.Management
{
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;

    // TODO: Implement all management with Adapater such as fallbacks, multi-publishing, load-balance, etc.
    internal class ProducingManager : IProducingManager
    {
        private readonly IMessageBrokerProducingAdapter _producingAdapter;

        public ProducingManager(IMessageBrokerProducingAdapter producingAdapter)
        {
            _producingAdapter = producingAdapter ?? throw new ArgumentNullException(nameof(producingAdapter));
        }

        public ValueTask PushAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
            => PushAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

        public ValueTask PushAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
            => _producingAdapter.PublishMessageAsync(payload, route, cancellationToken);

        public ValueTask PushRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PushRawAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

        public ValueTask PushRawAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
            => _producingAdapter.PublishRawMessageAsync(message, route, cancellationToken);
    }
}
