namespace Pigeon.Messaging.InMemory.Tests
{
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class RetryTestDecisionInterceptor : IConsumeDecisionInterceptor
    {
        public ValueTask<PigeonConsumeDecisionResult> InterceptAsync(ConsumeContext context, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
    }
}
