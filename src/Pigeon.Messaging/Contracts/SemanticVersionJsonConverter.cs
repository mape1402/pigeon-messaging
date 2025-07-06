namespace Pigeon.Messaging.Contracts
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A custom JSON converter for the <see cref="SemanticVersion"/> type.
    /// It handles serialization and deserialization of semantic version objects
    /// to and from their string representation.
    /// </summary>
    public class SemanticVersionJsonConverter : JsonConverter<SemanticVersion>
    {
        /// <summary>
        /// Reads and converts the JSON string value into a <see cref="SemanticVersion"/> instance.
        /// </summary>
        /// <param name="reader">The UTF-8 JSON reader.</param>
        /// <param name="typeToConvert">The type to convert (should be SemanticVersion).</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>A <see cref="SemanticVersion"/> parsed from the JSON string.</returns>
        public override SemanticVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();
            return SemanticVersion.Parse(SemanticVersion.Parse(value));
        }

        /// <summary>
        /// Writes a <see cref="SemanticVersion"/> instance as a JSON string.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The <see cref="SemanticVersion"/> value to write.</param>
        /// <param name="options">Serializer options.</param>
        public override void Write(Utf8JsonWriter writer, SemanticVersion value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
