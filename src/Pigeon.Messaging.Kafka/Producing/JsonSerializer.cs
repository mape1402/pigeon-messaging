namespace Pigeon.Messaging.Kafka.Producing
{
    using Confluent.Kafka;

    /// <summary>
    /// Provides a JSON serializer for Kafka messages, using System.Text.Json and supporting SemanticVersion conversion.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    internal sealed class JsonSerializer<T> : ISerializer<T>
    {
        private readonly ISerializer _serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonSerializer{T}"/> class.
        /// </summary>
        /// <param name="serializer">The serializer instance to use for serialization.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serializer"/> is null.</exception>
        public JsonSerializer(ISerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// Serializes the specified data to a UTF-8 encoded JSON byte array for Kafka transport.
        /// </summary>
        /// <param name="data">The data to serialize.</param>
        /// <param name="context">The serialization context provided by Kafka.</param>
        /// <returns>A byte array containing the UTF-8 encoded JSON representation of the data.</returns>
        public byte[] Serialize(T data, SerializationContext context)
            => _serializer.SerializeAsBytes(data);
    }
}
