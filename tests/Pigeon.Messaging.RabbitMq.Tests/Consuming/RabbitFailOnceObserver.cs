using Pigeon.Messaging.Consuming.Dispatching;

namespace Pigeon.Messaging.RabbitMq.Tests.Consuming;

internal sealed class RabbitFailOnceObserver(RabbitObserverState state) : IConsumeDecisionObserver
{
    public ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref state.Attempts) == 1)
            throw new InvalidOperationException("Simulated critical observer failure.");
        state.Recovered.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
