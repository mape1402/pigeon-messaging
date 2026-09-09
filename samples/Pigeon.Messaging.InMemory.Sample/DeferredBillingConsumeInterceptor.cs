namespace Pigeon.Messaging.InMemory.Sample
{
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class DeferredBillingConsumeInterceptor : IConsumeDecisionInterceptor
    {
        private readonly InMemorySampleScenario _scenario;
        private readonly IPigeonConsumeEnvelopeFactory _envelopeFactory;

        public DeferredBillingConsumeInterceptor(
            InMemorySampleScenario scenario,
            IPigeonConsumeEnvelopeFactory envelopeFactory)
        {
            _scenario = scenario;
            _envelopeFactory = envelopeFactory;
        }

        public ValueTask<PigeonConsumeDecisionResult> InterceptAsync(
            ConsumeContext context,
            CancellationToken cancellationToken = default)
        {
            if (context.ExecutionSource == ConsumeExecutionSource.DeferredReplay)
                return ValueTask.FromResult(PigeonConsumeDecisionResult.Continue);

            _scenario.CaptureDeferredEnvelope(_envelopeFactory.Create(context));

            return ValueTask.FromResult(new PigeonConsumeDecisionResult(
                PigeonConsumeDecision.Defer,
                "Billing work was scheduled for durable replay."));
        }
    }
}
