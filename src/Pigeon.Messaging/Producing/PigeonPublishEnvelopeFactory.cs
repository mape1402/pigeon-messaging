namespace Pigeon.Messaging.Producing
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Contracts;
    using System.Text;

    internal sealed class PigeonPublishEnvelopeFactory : IPigeonPublishEnvelopeFactory
    {
        private readonly ISerializer _serializer;
        private readonly GlobalSettings _settings;

        public PigeonPublishEnvelopeFactory(
            ISerializer serializer,
            IOptions<GlobalSettings> settings)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public ValueTask<PigeonPublishEnvelope> CreateAsync(
            PublishContext context,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            cancellationToken.ThrowIfCancellationRequested();

            var payload = context.IsRaw
                ? context.Message
                : CreateWrappedPayload(context);

            var serialized = _serializer.Serialize(payload);
            var route = context.Route ?? throw new InvalidOperationException("Publish context route is required to create an envelope.");

            return ValueTask.FromResult(new PigeonPublishEnvelope
            {
                Transport = string.IsNullOrWhiteSpace(context.Transport) ? "pigeon" : context.Transport,
                Topic = route.Topic,
                Version = context.Version.ToString(),
                Operation = context.Operation ?? context.MessageType?.Name,
                Destination = ResolveDestination(route),
                Exchange = route.Exchange,
                RoutingKey = route.RoutingKey,
                ContentType = string.IsNullOrWhiteSpace(context.ContentType) ? "application/json" : context.ContentType,
                Payload = Encoding.UTF8.GetBytes(serialized),
                PayloadType = payload.GetType().AssemblyQualifiedName,
                Headers = new Dictionary<string, string>(context.Headers, StringComparer.OrdinalIgnoreCase),
                Metadata = ToStringDictionary(context.Metadata),
                CorrelationId = context.CorrelationId ?? ResolveString(context.Metadata, "correlation-id", "correlationId"),
                TraceId = context.TraceId ?? ResolveString(context.Metadata, "trace-id", "traceId"),
                IsRaw = context.IsRaw
            });
        }

        private object CreateWrappedPayload(PublishContext context)
        {
            var wrappedType = typeof(WrappedPayload<>).MakeGenericType(context.MessageType);
            var wrappedPayload = Activator.CreateInstance(wrappedType);

            wrappedType.GetProperty(nameof(WrappedPayload<object>.CreatedOnUtc))!
                .SetValue(wrappedPayload, DateTimeOffset.UtcNow);
            wrappedType.GetProperty(nameof(WrappedPayload<object>.Message))!
                .SetValue(wrappedPayload, context.Message);
            wrappedType.GetProperty(nameof(WrappedPayload<object>.MessageVersion))!
                .SetValue(wrappedPayload, context.Version);
            wrappedType.GetProperty(nameof(WrappedPayload<object>.Metadata))!
                .SetValue(wrappedPayload, context.Metadata);
            wrappedType.GetProperty(nameof(WrappedPayload<object>.Domain))!
                .SetValue(wrappedPayload, _settings.Domain);

            return wrappedPayload;
        }

        private static string ResolveDestination(PublishingRoute route)
        {
            if (!string.IsNullOrWhiteSpace(route.Exchange))
                return $"{route.Exchange}:{route.RoutingKey}";

            return route.Topic;
        }

        private static Dictionary<string, string> ToStringDictionary(IReadOnlyDictionary<string, object> metadata)
            => metadata is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : metadata.ToDictionary(
                    item => item.Key,
                    item => item.Value?.ToString(),
                    StringComparer.OrdinalIgnoreCase);

        private static string ResolveString(
            IReadOnlyDictionary<string, object> metadata,
            params string[] keys)
        {
            if (metadata == null)
                return null;

            foreach (var key in keys)
            {
                if (metadata.TryGetValue(key, out var value))
                    return value?.ToString();
            }

            return null;
        }
    }
}
