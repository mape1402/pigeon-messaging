namespace Pigeon.Messaging.Producing
{
    using Pigeon.Messaging.Contracts;
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Context object passed to publish interceptors, allowing
    /// addition of metadata to enrich or modify the message before publishing.
    /// </summary>
    public class PublishContext
    {
        private readonly ConcurrentDictionary<string, object> _metadata = new();
        private readonly ConcurrentDictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the message instance being published.
        /// </summary>
        public object Message { get; init; }

        /// <summary>
        /// Gets the message CLR type.
        /// </summary>
        public Type MessageType { get; init; }

        /// <summary>
        /// Gets the route selected for the publish operation.
        /// </summary>
        public PublishingRoute Route { get; internal set; }

        /// <summary>
        /// Gets the message semantic version.
        /// </summary>
        public SemanticVersion Version { get; init; }

        /// <summary>
        /// Gets a value indicating whether the message will be published without a Pigeon wrapper.
        /// </summary>
        public bool IsRaw { get; init; }

        /// <summary>
        /// Gets or sets the optional logical transport name used by external durable integrations.
        /// </summary>
        public string Transport { get; set; } = "pigeon";

        /// <summary>
        /// Gets or sets the optional logical operation name for the publish operation.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Gets or sets the payload content type.
        /// </summary>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// Gets or sets the optional correlation identifier propagated with the publish operation.
        /// </summary>
        public string CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the optional trace identifier propagated with the publish operation.
        /// </summary>
        public string TraceId { get; set; }

        /// <summary>
        /// Gets the current metadata attached to the publish operation.
        /// </summary>
        public IReadOnlyDictionary<string, object> Metadata
            => new ReadOnlyDictionary<string, object>(_metadata);

        /// <summary>
        /// Gets the transport headers attached to the publish operation.
        /// </summary>
        public IReadOnlyDictionary<string, string> Headers
            => new ReadOnlyDictionary<string, string>(_headers);

        /// <summary>
        /// Adds a metadata entry with the specified key and value.
        /// Throws <see cref="InvalidOperationException"/> if the key already exists.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value.</typeparam>
        /// <param name="key">The unique key identifying the metadata.</param>
        /// <param name="value">The metadata value to add.</param>
        /// <exception cref="InvalidOperationException">Thrown if the key already exists.</exception>
        public void AddMetadata<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            if (!_metadata.TryAdd(key, value))
                throw new InvalidOperationException($"RawMetadata with key '{key}' already exists.");
        }

        /// <summary>
        /// Adds a transport header with the specified key and value.
        /// </summary>
        /// <param name="key">The unique header name.</param>
        /// <param name="value">The header value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the header already exists.</exception>
        public void AddHeader(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentNullException(nameof(key));

            if (!_headers.TryAdd(key, value))
                throw new InvalidOperationException($"Header with key '{key}' already exists.");
        }

        /// <summary>
        /// Retrieves a read-only view of the current metadata dictionary.
        /// This prevents external code from modifying the internal metadata directly.
        /// </summary>
        /// <returns>
        /// An <see cref="IReadOnlyDictionary{String, Object}"/> containing
        /// the metadata key-value pairs.
        /// </returns>
        internal IReadOnlyDictionary<string, object> GetMetadata()
            => Metadata;

        internal void MergeMetadata(IReadOnlyDictionary<string, object> metadata)
        {
            if (metadata == null)
                return;

            foreach (var item in metadata)
                _metadata[item.Key] = item.Value;
        }

        internal void MergeHeaders(IReadOnlyDictionary<string, string> headers)
        {
            if (headers == null)
                return;

            foreach (var item in headers)
                _headers[item.Key] = item.Value;
        }
    }
}
