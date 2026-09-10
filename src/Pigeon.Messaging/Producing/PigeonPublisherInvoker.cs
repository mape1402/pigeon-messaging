namespace Pigeon.Messaging.Producing
{
    using Pigeon.Messaging.Producing.Management;
    using System.Reflection;
    using System.Text;

    internal sealed class PigeonPublisherInvoker : IPigeonPublisherInvoker
    {
        private readonly IProducingManager _producingManager;
        private readonly ISerializer _serializer;

        public PigeonPublisherInvoker(
            IProducingManager producingManager,
            ISerializer serializer)
        {
            _producingManager = producingManager ?? throw new ArgumentNullException(nameof(producingManager));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public async ValueTask PublishAsync(
            PigeonPublishEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            var payloadType = ResolveType(envelope.PayloadType);
            var rawJson = Encoding.UTF8.GetString(envelope.Payload ?? Array.Empty<byte>());
            var payload = _serializer.Deserialize(rawJson, payloadType);
            var route = ResolveRoute(envelope);

            if (envelope.IsRaw)
            {
                await InvokeGenericPushAsync(
                    nameof(IProducingManager.PushRawAsync),
                    payloadType,
                    payload,
                    route,
                    cancellationToken);
                return;
            }

            var messageType = payloadType.GetGenericArguments().Single();
            await InvokeGenericPushAsync(
                nameof(IProducingManager.PushAsync),
                messageType,
                payload,
                route,
                cancellationToken);
        }

        private async ValueTask InvokeGenericPushAsync(
            string methodName,
            Type genericType,
            object payload,
            PublishingRoute route,
            CancellationToken cancellationToken)
        {
            var method = typeof(IProducingManager)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(method =>
                    method.Name == methodName &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 3 &&
                    method.GetParameters()[1].ParameterType == typeof(PublishingRoute));

            var result = method
                .MakeGenericMethod(genericType)
                .Invoke(_producingManager, new[] { payload, route, cancellationToken });

            await (ValueTask)result;
        }

        private static PublishingRoute ResolveRoute(PigeonPublishEnvelope envelope)
            => !string.IsNullOrWhiteSpace(envelope.Exchange)
                ? PublishingRoute.ForExchange(envelope.Exchange, envelope.RoutingKey)
                : PublishingRoute.ForTopic(envelope.Topic);

        private static Type ResolveType(string typeName)
            => !string.IsNullOrWhiteSpace(typeName) && Type.GetType(typeName) is { } type
                ? type
                : throw new InvalidOperationException($"Pigeon publish envelope payload type '{typeName}' could not be resolved.");
    }
}
