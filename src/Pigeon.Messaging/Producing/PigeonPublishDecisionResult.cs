namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Result returned by a publish decision interceptor.
    /// </summary>
    public sealed class PigeonPublishDecisionResult
    {
        /// <summary>
        /// Gets a reusable continue result.
        /// </summary>
        public static PigeonPublishDecisionResult Continue { get; } = new(PigeonPublishDecision.Continue);

        /// <summary>
        /// Initializes a new instance of the <see cref="PigeonPublishDecisionResult"/> class.
        /// </summary>
        /// <param name="decision">The selected publish decision.</param>
        /// <param name="reason">The optional decision reason.</param>
        public PigeonPublishDecisionResult(PigeonPublishDecision decision, string reason = null)
        {
            Decision = decision;
            Reason = reason;
        }

        /// <summary>
        /// Gets the selected publish decision.
        /// </summary>
        public PigeonPublishDecision Decision { get; }

        /// <summary>
        /// Gets an optional replacement route.
        /// </summary>
        public PublishingRoute Route { get; init; }

        /// <summary>
        /// Gets the optional decision reason.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Gets metadata emitted by the decision interceptor.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; init; } =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }
}
