namespace Pigeon.Messaging.Consuming
{
    using Pigeon.Messaging.Contracts;
    using System;
    using System.Text.Json;

    /// <summary>
    /// Represents a lightweight view over a raw JSON payload,
    /// allowing extraction of common envelope information (domain, version, timestamp)
    /// without fully deserializing the entire message.
    /// </summary>
    public readonly struct RawPayload
    {
        /// <summary>
        /// Gets the logical domain the message belongs to.
        /// </summary>
        public string Domain { get; }

        /// <summary>
        /// Gets the semantic version of the message contract.
        /// </summary>
        public SemanticVersion MessageVersion { get; }

        /// <summary>
        /// Gets the UTC timestamp when the message was created.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; }

        /// <summary>
        /// Gets the original JSON string of the full payload.
        /// </summary>
        public string RawJson { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="RawPayload"/> by
        /// parsing only the required fields from a JSON string.
        /// </summary>
        /// <param name="json">The raw JSON payload string.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the input JSON is null or whitespace.
        /// </exception>
        /// <exception cref="JsonException">
        /// Thrown if any of the expected properties are missing or invalid.
        /// </exception>
        public RawPayload(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            RawJson = json;

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("Domain", out var domainProp) || domainProp.ValueKind != JsonValueKind.String)
                throw new JsonException("Missing or invalid 'Domain' property.");

            Domain = domainProp.GetString()!;

            if (!root.TryGetProperty("MessageVersion", out var versionProp) || versionProp.ValueKind != JsonValueKind.String)
                throw new JsonException("Missing or invalid 'MessageVersion' property.");

            MessageVersion = versionProp.GetString();

            if (!root.TryGetProperty("CreatedOnUtc", out var createdProp) || createdProp.ValueKind != JsonValueKind.String)
                throw new JsonException("Missing or invalid 'CreatedOnUtc' property.");

            CreatedOnUtc = DateTimeOffset.Parse(createdProp.GetString()!);
        }

        /// <summary>
        /// Extracts and deserializes the 'Message' part of the payload
        /// to the given type.
        /// </summary>
        /// <param name="messageType">The type to which the 'Message' node should be deserialized.</param>
        /// <param name="serializer">The serializer to use for deserialization.</param>
        /// <returns>The deserialized message as an <see cref="object"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageType"/> is null.</exception>
        /// <exception cref="JsonException">
        /// Thrown if the 'Message' node is missing or invalid.
        /// </exception>
        public object GetMessage(Type messageType, ISerializer serializer)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            using var document = JsonDocument.Parse(RawJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("Message", out var messageProp))
                throw new JsonException("Missing 'Message' property in payload.");

            var rawMessageJson = messageProp.GetRawText();
            return serializer.Deserialize(rawMessageJson, messageType);
        }

        /// <summary>
        /// Parses the raw JSON payload and extracts the 'Metadata' section
        /// as a dictionary of string values containing the raw JSON for each metadata entry.
        /// </summary>
        /// <returns>
        /// A read-only dictionary containing metadata keys and their raw JSON string values.
        /// If the 'Metadata' node is missing, an empty dictionary is returned.
        /// </returns>
        /// <remarks>
        /// This method does not fully deserialize the metadata values.
        /// Consumers can use the raw JSON string to deserialize each value
        /// lazily and safely to the desired type when needed.
        /// </remarks>
        public IReadOnlyDictionary<string, string> GetMetadata()
        {
            using var doc = JsonDocument.Parse(RawJson);

            if (!doc.RootElement.TryGetProperty("Metadata", out var metaElement))
                return new Dictionary<string, string>();

            var dict = new Dictionary<string, string>();

            foreach (var prop in metaElement.EnumerateObject())
                dict[prop.Name] = prop.Value.GetRawText();

            return dict;
        }
    }
}
