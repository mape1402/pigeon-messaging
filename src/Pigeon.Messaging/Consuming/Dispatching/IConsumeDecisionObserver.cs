namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Observes terminal consume decisions before settlement. Implementations must be idempotent.</summary>
    public interface IConsumeDecisionObserver
    {
        /// <summary>Observes a terminal consume decision.</summary>
        /// <param name="observation">The original context and complete decision result.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A value task representing the observation.</returns>
        ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default);
    }
}
