namespace Pigeon.Messaging.Producing.Management
{
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;
    using System.Reflection;

    // TODO: Implement all management with Adapater such as fallbacks, multi-publishing, load-balance, etc.
    internal class ProducingManager : IProducingManager
    {
        private readonly IMessageBrokerProducingAdapter _producingAdapter;
        private readonly ITopologyProvisioningService _topologyProvisioningService;
        private readonly ISerializer _serializer;

        public ProducingManager(IMessageBrokerProducingAdapter producingAdapter)
            : this(producingAdapter, NoopTopologyProvisioningService.Instance, null)
        {
        }

        public ProducingManager(IMessageBrokerProducingAdapter producingAdapter, ITopologyProvisioningService topologyProvisioningService)
            : this(producingAdapter, topologyProvisioningService, null)
        {
        }

        public ProducingManager(
            IMessageBrokerProducingAdapter producingAdapter,
            ITopologyProvisioningService topologyProvisioningService,
            ISerializer serializer)
        {
            _producingAdapter = producingAdapter ?? throw new ArgumentNullException(nameof(producingAdapter));
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
            _serializer = serializer;
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

        public async ValueTask PushOutboxAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (_serializer == null)
                throw new InvalidOperationException("An ISerializer is required to publish outbox messages.");

            var payloadType = Type.GetType(message.PayloadType, throwOnError: true);
            var payload = _serializer.Deserialize(message.Payload, payloadType);
            var route = !string.IsNullOrWhiteSpace(message.Exchange)
                ? PublishingRoute.ForExchange(message.Exchange, message.RoutingKey)
                : PublishingRoute.ForTopic(message.Topic);

            if (message.IsRaw)
            {
                await InvokeGenericPushAsync(nameof(PushRawAsync), payloadType, payload, route, cancellationToken);
                return;
            }

            var messageType = payloadType.GetGenericArguments().Single();
            await InvokeGenericPushAsync(nameof(PushAsync), messageType, payload, route, cancellationToken);
        }

        private async ValueTask InvokeGenericPushAsync(
            string methodName,
            Type genericType,
            object payload,
            PublishingRoute route,
            CancellationToken cancellationToken)
        {
            var method = typeof(ProducingManager)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(m =>
                    m.Name == methodName &&
                    m.IsGenericMethodDefinition &&
                    m.GetParameters().Length == 3 &&
                    m.GetParameters()[1].ParameterType == typeof(PublishingRoute));

            var result = method
                .MakeGenericMethod(genericType)
                .Invoke(this, new[] { payload, route, cancellationToken });

            await (ValueTask)result;
        }
    }
}
