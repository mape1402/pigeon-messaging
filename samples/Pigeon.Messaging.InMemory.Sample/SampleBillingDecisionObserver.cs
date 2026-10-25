namespace Pigeon.Messaging.InMemory.Sample
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class SampleBillingDecisionObserver(InMemorySampleScenario scenario, ILogger<SampleBillingDecisionObserver> logger) : IConsumeDecisionObserver
    {
        public ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default)
        {
            observation.Context.ReplyMetadata.TryGetValue("billing-status", out var status);
            logger.LogInformation("Billing decision metadata: {Status}.", status);
            scenario.MarkDecisionObserved();
            return ValueTask.CompletedTask;
        }
    }
}
