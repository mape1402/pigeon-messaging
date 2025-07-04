namespace Pigeon.Messaging.Producing.Management
{
    using Pigeon.Messaging.Contracts;

    // TODO: Implement all management with Adapater such as fallbacks, multi-publishing, load-balance, etc.
    internal class ProducingManager : IProducingManager
    {
        private readonly IMessageBrokerProducingAdapter _producingAdapter;

        public ProducingManager(IMessageBrokerProducingAdapter producingAdapter)
        {
            _producingAdapter = producingAdapter ?? throw new ArgumentNullException(nameof(producingAdapter));
        }

        public ValueTask PushAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
            => _producingAdapter.PublishMessageAsync(payload, topic, cancellationToken);
    }
}
