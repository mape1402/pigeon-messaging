namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Contains the original message context and its terminal decision.</summary>
    /// <param name="Context">The current message context.</param>
    /// <param name="Result">The terminal decision result.</param>
    public sealed record ConsumeDecisionObservation(ConsumeContext Context, PigeonConsumeDecisionResult Result);
}
