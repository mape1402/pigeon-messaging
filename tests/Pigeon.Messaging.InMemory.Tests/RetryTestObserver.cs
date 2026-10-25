namespace Pigeon.Messaging.InMemory.Tests
{
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class RetryTestObserver(RetryObserverState state) : IConsumeDecisionObserver
    {
        public ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default)
        {
            if (observation.Context.Subscription == "billing" && Interlocked.Increment(ref state.BillingAttempts) == 1)
                throw new InvalidOperationException("Simulated observer failure.");
            return ValueTask.CompletedTask;
        }
    }
}
