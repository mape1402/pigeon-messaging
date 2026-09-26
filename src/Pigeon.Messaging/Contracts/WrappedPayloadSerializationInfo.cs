namespace Pigeon.Messaging.Contracts
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Describes the serialization format used by a Pigeon wrapped payload.
    /// </summary>
    public sealed class WrappedPayloadSerializationInfo
    {
        /// <summary>
        /// The current wrapped payload format version.
        /// </summary>
        public const string CurrentFormatVersion = "1.0";

        /// <summary>
        /// Gets the default serialization info used by legacy-compatible payloads.
        /// </summary>
        public static WrappedPayloadSerializationInfo Default { get; } = new()
        {
            FormatVersion = CurrentFormatVersion,
            PropertyNamingPolicy = WrappedPayloadJsonPolicyNames.Default
        };

        /// <summary>
        /// Gets the wrapped payload format version.
        /// </summary>
        [JsonPropertyName("formatVersion")]
        public string FormatVersion { get; init; } = CurrentFormatVersion;

        /// <summary>
        /// Gets the JSON property naming policy used for the wrapped payload body.
        /// </summary>
        [JsonPropertyName("propertyNamingPolicy")]
        public string PropertyNamingPolicy { get; init; } = WrappedPayloadJsonPolicyNames.Default;
    }
}
