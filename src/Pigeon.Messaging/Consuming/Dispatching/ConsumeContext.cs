namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Pigeon.Messaging.Contracts;
    using System.Collections.Concurrent;
    using System.Text.Json;

    /// <summary>
    /// Represents the contextual information for processing a consumed message,
    /// including the deserialized payload, envelope metadata, topic, version,
    /// and access to scoped services.
    /// </summary>
    /// <remarks>
    /// A single <see cref="ConsumeContext"/> is created for each message instance
    /// during consumption and flows through the entire interceptor pipeline and handler.
    /// Interceptors and handlers can use the context to access dependency injection,
    /// inspect message details, or retrieve metadata attached by the publisher.
    /// </remarks>
    public class ConsumeContext
    {
        private readonly ConcurrentDictionary<string, object> _metadata = new();

        /// <summary>
        /// Provides access to scoped services.
        /// </summary>
        public IServiceProvider Services { get; init; }

        /// <summary>
        /// Logical topic or channel this message was published to.
        /// </summary>
        public string Topic { get; init; }

        /// <summary>
        /// Version of the message contract.
        /// </summary>
        public SemanticVersion MessageVersion { get; init; }

        /// <summary>
        /// UTC timestamp when the message was created.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; init; }

        /// <summary>
        /// Logical source domain.
        /// </summary>
        public string From { get; init; }

        /// <summary>
        /// The deserialized message payload.
        /// </summary>
        public object Message { get; init; }

        /// <summary>
        /// The CLR type of the message.
        /// </summary>
        public Type MessageType { get; init; }

        /// <summary>
        /// The cancellation token for cooperative cancellation.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>
        /// Optional metadata attached to the message envelope.
        /// </summary>
        public IReadOnlyDictionary<string, object> RawMetadata { get; init; }

        /// <summary>
        /// Retrieves a strongly typed metadata value by key.
        /// Throws if the key does not exist or cannot be cast to the target type.
        /// </summary>
        /// <typeparam name="T">The expected type of the metadata value.</typeparam>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value cast to type <typeparamref name="T"/>.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if the key does not exist in the metadata.
        /// </exception>
        /// <exception cref="InvalidCastException">
        /// Thrown if the metadata value cannot be cast to the specified type.
        /// </exception>
        public T GetMetadata<T>(string key)
        {
            if (_metadata.TryGetValue(key, out object tValue))
                return (T)tValue;

            if (!RawMetadata.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found.");

            if (value is T typed)
                return typed;

            if (value is JsonElement je)
            {
                var typedValue = je.Deserialize<T>();

                _metadata.TryAdd(key, typedValue);
                return typedValue!;
            }

            throw new InvalidCastException($"Cannot cast value of key '{key}' to type '{typeof(T).FullName}'.");
        }
    }
}
