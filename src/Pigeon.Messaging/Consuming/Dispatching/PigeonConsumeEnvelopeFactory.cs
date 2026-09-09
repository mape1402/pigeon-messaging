namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;
    using System.Text;

    internal sealed class PigeonConsumeEnvelopeFactory : IPigeonConsumeEnvelopeFactory
    {
        private readonly ISerializer _serializer;

        public PigeonConsumeEnvelopeFactory(ISerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public PigeonConsumeEnvelope Create(ConsumeContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var payload = _serializer.Serialize(context.Message);

            return new PigeonConsumeEnvelope
            {
                MessageId = context.MessageId,
                CorrelationId = context.CorrelationId,
                Topic = context.Topic,
                Operation = context.Operation,
                Version = context.MessageVersion,
                Subscription = context.Subscription,
                PayloadType = context.MessageType.AssemblyQualifiedName,
                Payload = Encoding.UTF8.GetBytes(payload),
                ContentType = "application/json",
                CreatedOnUtc = context.CreatedOnUtc,
                From = context.From,
                Headers = new Dictionary<string, string>(context.Headers, StringComparer.OrdinalIgnoreCase),
                Metadata = new Dictionary<string, string>(context.RawMetadata, StringComparer.OrdinalIgnoreCase)
            };
        }

        public ConsumeContext CreateContext(
            PigeonConsumeEnvelope envelope,
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            if (envelope == null)
                throw new ArgumentNullException(nameof(envelope));

            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var consumingConfigurator = services.GetRequiredService<IConsumingConfigurator>();
            var subscription = string.IsNullOrWhiteSpace(envelope.Subscription)
                ? ConsumerEndpoint.DefaultSubscription
                : envelope.Subscription;

            var configuration = consumingConfigurator.GetConfiguration(envelope.Topic, envelope.Version, subscription);
            configuration ??= consumingConfigurator.GetConfiguration(envelope.Topic, envelope.Version);

            if (configuration == null)
                throw new InvalidOperationException($"No consumer configuration found for topic '{envelope.Topic}', version '{envelope.Version}', subscription '{subscription}'.");

            var rawJson = Encoding.UTF8.GetString(envelope.Payload ?? Array.Empty<byte>());
            var message = _serializer.Deserialize(rawJson, configuration.MessageType);

            return new ConsumeContext
            {
                CancellationToken = cancellationToken,
                CreatedOnUtc = envelope.CreatedOnUtc == default ? DateTimeOffset.UtcNow : envelope.CreatedOnUtc,
                ExecutionSource = ConsumeExecutionSource.DeferredReplay,
                From = envelope.From,
                Headers = envelope.Headers,
                Message = message,
                MessageId = envelope.MessageId,
                MessageType = configuration.MessageType,
                MessageVersion = configuration.Version,
                Operation = envelope.Operation,
                RawMetadata = envelope.Metadata,
                Services = services,
                Subscription = configuration.Subscription,
                Topic = envelope.Topic,
                CorrelationId = envelope.CorrelationId
            };
        }
    }
}
