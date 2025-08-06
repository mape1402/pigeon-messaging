namespace Pigeon.Messaging
{
    /// <summary>
    /// Interface for serializing and deserializing message payloads.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serializes the specified payload to a JSON string using the configured serializer options.
        /// </summary>
        /// <param name="payload">The payload to serialize.</param>
        /// <returns>The serialized JSON string.</returns>
        string Serialize(object payload);

        /// <summary>
        /// Deserializes the specified raw JSON string to an object of the given target type using System.Text.Json.
        /// </summary>
        /// <param name="rawJson">The raw JSON string to deserialize.</param>
        /// <param name="targetType">The target type to deserialize to.</param>
        /// <returns>The deserialized object.</returns>
        object Deserialize(string rawJson, Type targetType);
    }
}
