namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Describes the semantic outcome independently of broker settlement.</summary>
    public sealed record PigeonConsumeOutcome
    {
        /// <summary>Gets the semantic outcome kind.</summary>
        public PigeonConsumeOutcomeKind Kind { get; init; }

        /// <summary>Gets optional failure details.</summary>
        public PigeonConsumeFailure Failure { get; init; }
    }
}
