namespace Pigeon.Messaging.Producing.Management
{
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    // TODO: Implement all management with Adapater such as fallbacks, multi-publishing, load-balance, etc.
    internal class ProducingManager : IProducingManager
    {
        private readonly IMessageBrokerProducingAdapter _producingAdapter;
        private readonly ITopologyProvisioningService _topologyProvisioningService;

        public ProducingManager(IMessageBrokerProducingAdapter producingAdapter)
            : this(producingAdapter, NoopTopologyProvisioningService.Instance)
        {
        }

        public ProducingManager(IMessageBrokerProducingAdapter producingAdapter, ITopologyProvisioningService topologyProvisioningService)
        {
            _producingAdapter = producingAdapter ?? throw new ArgumentNullException(nameof(producingAdapter));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
        }

        public ValueTask PushAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default) where T : class
            => PushAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

        public async ValueTask PushAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
        {
            await _topologyProvisioningService.EnsurePublishTopologyAsync(route, cancellationToken);
            await _producingAdapter.PublishMessageAsync(payload, route, cancellationToken);
        }

        public ValueTask PushRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class
            => PushRawAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

        public async ValueTask PushRawAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default) where T : class
        {
            await _topologyProvisioningService.EnsurePublishTopologyAsync(route, cancellationToken);
            await _producingAdapter.PublishRawMessageAsync(message, route, cancellationToken);
        }
    }
}
