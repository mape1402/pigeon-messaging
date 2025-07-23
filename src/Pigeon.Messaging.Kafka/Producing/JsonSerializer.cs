namespace Pigeon.Messaging.Kafka.Producing
{
    using Confluent.Kafka;
    using Pigeon.Messaging.Contracts;
    using System.Text;
    using System.Text.Json;

    /// <summary>
    /// Provides a JSON serializer for Kafka messages, using System.Text.Json and supporting SemanticVersion conversion.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    internal sealed class JsonSerializer<T> : ISerializer<T>
    {
        /// <summary>
        /// Serializes the specified data to a UTF-8 encoded JSON byte array for Kafka transport.
        /// </summary>
        /// <param name="data">The data to serialize.</param>
        /// <param name="context">The serialization context provided by Kafka.</param>
        /// <returns>A byte array containing the UTF-8 encoded JSON representation of the data.</returns>
        public byte[] Serialize(T data, SerializationContext context)
        {
            var jsonSerializerOptions = new JsonSerializerOptions();
            jsonSerializerOptions.Converters.Add(new SemanticVersionJsonConverter());

            var json = JsonSerializer.Serialize(data, jsonSerializerOptions);

            return Encoding.UTF8.GetBytes(json);
        }
    }
}
