using Pigeon.Messaging.Consuming.Dispatching;

namespace Pigeon.Messaging.RabbitMq.Tests.Consuming;

internal sealed class RabbitObserverDecisionInterceptor : IConsumeDecisionInterceptor
{
    public ValueTask<PigeonConsumeDecisionResult> InterceptAsync(ConsumeContext context, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
}
