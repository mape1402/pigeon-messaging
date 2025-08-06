namespace Pigeon.Messaging
{
    using System.Text.Json;

    /// <summary>
    /// Default implementation of <see cref="ISerializer"/> using System.Text.Json for serialization and UTF-8 encoding.
    /// </summary>
    internal sealed class DefaultSerializer : ISerializer
    {
        private readonly JsonSerializerOptions _serializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultSerializer"/> class.
        /// </summary>
        /// <param name="serializerOptions">The JSON serializer options to use for serialization.</param>
        public DefaultSerializer(JsonSerializerOptions serializerOptions)
        {
            _serializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
        }

        /// <summary>
        /// Serializes the specified payload to a JSON string using the configured serializer options.
        /// </summary>
        /// <param name="payload">The payload to serialize.</param>
        /// <returns>The serialized JSON string.</returns>
        public string Serialize(object payload)
            => JsonSerializer.Serialize(payload, _serializerOptions);

        /// <summary>
        /// Deserializes the specified raw JSON string to an object of the given target type using System.Text.Json.
        /// </summary>
        /// <param name="rawJson">The raw JSON string to deserialize.</param>
        /// <param name="targetType">The target type to deserialize to.</param>
        /// <returns>The deserialized object.</returns>
        public object Deserialize(string rawJson, Type targetType)
            => JsonSerializer.Deserialize(rawJson, targetType, _serializerOptions);
    }
}
