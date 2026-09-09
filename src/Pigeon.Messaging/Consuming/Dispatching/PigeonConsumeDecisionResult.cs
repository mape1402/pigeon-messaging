namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Result returned by a consume decision interceptor.
    /// </summary>
    public sealed class PigeonConsumeDecisionResult
    {
        /// <summary>
        /// Gets a reusable continue result.
        /// </summary>
        public static PigeonConsumeDecisionResult Continue { get; } = new(PigeonConsumeDecision.Continue);

        /// <summary>
        /// Initializes a new instance of the <see cref="PigeonConsumeDecisionResult"/> class.
        /// </summary>
        /// <param name="decision">The selected consume decision.</param>
        /// <param name="reason">The optional decision reason.</param>
        public PigeonConsumeDecisionResult(PigeonConsumeDecision decision, string reason = null)
        {
            Decision = decision;
            Reason = reason;
        }

        /// <summary>
        /// Gets the selected consume decision.
        /// </summary>
        public PigeonConsumeDecision Decision { get; }

        /// <summary>
        /// Gets the optional decision reason.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets metadata emitted by the decision interceptor.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
