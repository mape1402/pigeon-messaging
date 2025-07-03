namespace Pigeon.Messaging.Contracts
{
    /// <summary>
    /// Represents a standardized message envelope for domain events or commands,
    /// providing tracing, versioning, and flexible metadata support.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the actual payload. Must be a class.
    /// </typeparam>
    public class WrappedPayload<T> where T : class
    {
        /// <summary>
        /// Logical domain or bounded context that the message belongs to.
        /// Useful for routing, segregation, or multi-tenant systems.
        /// </summary>
        public string Domain { get; init; }

        /// <summary>
        /// Semantic version of the message contract.
        /// Allows consumers to handle breaking or compatible changes.
        /// </summary>
        public SemanticVersion MessageVersion { get; init; }

        /// <summary>
        /// UTC timestamp indicating when the message was created.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; init; }

        /// <summary>
        /// The actual business message.
        /// </summary>
        public T Message { get; init; }

        /// <summary>
        /// Arbitrary key-value pairs for additional context,
        /// such as orchestration data, security claims, or custom extensions.
        /// Consumers are responsible for defining and interpreting keys.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata { get; init; }
    }
}
