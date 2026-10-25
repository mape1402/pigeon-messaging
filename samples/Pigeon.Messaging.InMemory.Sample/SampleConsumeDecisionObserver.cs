namespace Pigeon.Messaging.InMemory.Sample
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class SampleConsumeDecisionObserver(ILogger<SampleConsumeDecisionObserver> logger) : IConsumeDecisionObserver
    {
        public ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Observed {Decision} ({Outcome}) for {Topic}/{Subscription}.",
                observation.Result.Decision, observation.Result.Outcome?.Kind,
                observation.Context.Topic, observation.Context.Subscription);
            return ValueTask.CompletedTask;
        }
    }
}
