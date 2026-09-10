namespace Pigeon.Messaging.InMemory.Sample
{
    using Pigeon.Messaging.Producing;

    internal sealed class ExternalOutboxPublishDecisionInterceptor : IPublishDecisionInterceptor
    {
        private readonly IPigeonPublishEnvelopeFactory _envelopeFactory;
        private readonly InMemorySampleScenario _scenario;

        public ExternalOutboxPublishDecisionInterceptor(
            IPigeonPublishEnvelopeFactory envelopeFactory,
            InMemorySampleScenario scenario)
        {
            _envelopeFactory = envelopeFactory;
            _scenario = scenario;
        }

        public async ValueTask<PigeonPublishDecisionResult> InterceptAsync(
            PublishContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.Route.Topic != OrderRoutes.ExternalOutboxTopic)
                return PigeonPublishDecisionResult.Continue;

            context.AddMetadata("external-outbox", "captured");

            var envelope = await _envelopeFactory.CreateAsync(context, cancellationToken);
            _scenario.CaptureExternalOutboxEnvelope(envelope);

            return new PigeonPublishDecisionResult(
                PigeonPublishDecision.Skip,
                "The sample external outbox persisted the Pigeon publish envelope.");
        }
    }
}
