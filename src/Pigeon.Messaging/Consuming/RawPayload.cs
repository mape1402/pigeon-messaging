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
        private readonly WrappedPayloadJsonPropertyNames _propertyNames;

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
        /// parsing only the required fields from a JSON string using the legacy
        /// default Pigeon wrapped payload policy.
        /// </summary>
        /// <param name="json">The raw JSON payload string.</param>
        public RawPayload(string json)
            : this(json, WrappedPayloadJsonPolicy.Default.PropertyNames)
        {
        }

        internal RawPayload(string json, WrappedPayloadJsonPropertyNames propertyNames)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            RawJson = json;
            _propertyNames = propertyNames ?? throw new ArgumentNullException(nameof(propertyNames));

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var domainProp = GetRequiredProperty(root, _propertyNames.Domain, JsonValueKind.String);
            Domain = domainProp.GetString()!;

            var versionProp = GetRequiredProperty(root, _propertyNames.MessageVersion, JsonValueKind.String);
            MessageVersion = versionProp.GetString();

            var createdProp = GetRequiredProperty(root, _propertyNames.CreatedOnUtc, JsonValueKind.String);
            CreatedOnUtc = DateTimeOffset.Parse(createdProp.GetString()!);
        }

        /// <summary>
        /// Extracts and deserializes the message part of the payload
        /// to the given type.
        /// </summary>
        /// <param name="messageType">The type to which the message node should be deserialized.</param>
        /// <param name="serializer">The serializer to use for deserialization.</param>
        /// <returns>The deserialized message as an <see cref="object"/>.</returns>
        public object GetMessage(Type messageType, ISerializer serializer)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (serializer == null)
                throw new ArgumentNullException(nameof(serializer));

            using var document = JsonDocument.Parse(RawJson);
            var root = document.RootElement;

            if (!root.TryGetProperty(PropertyNames.Message, out var messageProp))
                throw new JsonException($"Missing '{PropertyNames.Message}' property in payload.");

            var rawMessageJson = messageProp.GetRawText();
            return serializer.Deserialize(rawMessageJson, messageType);
        }

        /// <summary>
        /// Parses the raw JSON payload and extracts the metadata section
        /// as a dictionary of string values containing the raw JSON for each metadata entry.
        /// </summary>
        /// <returns>
        /// A read-only dictionary containing metadata keys and their raw JSON string values.
        /// If the metadata node is missing, an empty dictionary is returned.
        /// </returns>
        public IReadOnlyDictionary<string, string> GetMetadata()
        {
            using var doc = JsonDocument.Parse(RawJson);

            if (!doc.RootElement.TryGetProperty(PropertyNames.Metadata, out var metaElement))
                return new Dictionary<string, string>();

            var dict = new Dictionary<string, string>();

            foreach (var prop in metaElement.EnumerateObject())
                dict[prop.Name] = prop.Value.GetRawText();

            return dict;
        }

        private WrappedPayloadJsonPropertyNames PropertyNames
            => _propertyNames ?? WrappedPayloadJsonPolicy.Default.PropertyNames;

        private static JsonElement GetRequiredProperty(
            JsonElement root,
            string propertyName,
            JsonValueKind expectedKind)
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != expectedKind)
                throw new JsonException($"Missing or invalid '{propertyName}' property.");

            return property;
        }
    }
}
