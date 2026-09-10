namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Represents a prepared publish operation that can be persisted and replayed later.
    /// </summary>
    public sealed class PigeonPublishEnvelope
    {
        /// <summary>
        /// Gets the logical transport name.
        /// </summary>
        public string Transport { get; init; } = "pigeon";

        /// <summary>
        /// Gets the logical topic.
        /// </summary>
        public string Topic { get; init; }

        /// <summary>
        /// Gets the semantic version for the published contract.
        /// </summary>
        public string Version { get; init; }

        /// <summary>
        /// Gets the optional logical operation.
        /// </summary>
        public string Operation { get; init; }

        /// <summary>
        /// Gets the effective destination used for diagnostics or external outbox indexing.
        /// </summary>
        public string Destination { get; init; }

        /// <summary>
        /// Gets the broker exchange or equivalent routed publishing channel.
        /// </summary>
        public string Exchange { get; init; }

        /// <summary>
        /// Gets the broker routing key.
        /// </summary>
        public string RoutingKey { get; init; }

        /// <summary>
        /// Gets the payload content type.
        /// </summary>
        public string ContentType { get; init; } = "application/json";

        /// <summary>
        /// Gets the serialized payload bytes that should be sent to the broker later.
        /// </summary>
        public byte[] Payload { get; init; } = Array.Empty<byte>();

        /// <summary>
        /// Gets the assembly-qualified CLR type used to deserialize <see cref="Payload"/>.
        /// </summary>
        public string PayloadType { get; init; }

        /// <summary>
        /// Gets transport headers captured with the publish operation.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets string metadata captured with the publish operation.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the optional correlation identifier.
        /// </summary>
        public string CorrelationId { get; init; }

        /// <summary>
        /// Gets the optional trace identifier.
        /// </summary>
        public string TraceId { get; init; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Payload"/> contains a raw broker payload.
        /// </summary>
        public bool IsRaw { get; init; }
    }
}
