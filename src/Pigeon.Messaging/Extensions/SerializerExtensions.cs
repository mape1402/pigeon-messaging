namespace Pigeon.Messaging
{
    using System.Text;

    /// <summary>
    /// Extension methods for the <see cref="ISerializer"/> interface.
    /// </summary>
    public static class SerializerExtensions
    {
        /// <summary>
        /// Serializes the specified payload to a JSON string and encodes it as a UTF-8 byte array.
        /// </summary>
        /// <param name="serializer">The serializer instance to use.</param>
        /// <param name="payload">The payload to serialize.</param>
        /// <returns>A byte array containing the UTF-8 encoded JSON representation of the payload.</returns>
        public static byte[] SerializeAsBytes(this ISerializer serializer, object payload)
        {
            var json = serializer.Serialize(payload);
            return Encoding.UTF8.GetBytes(json);
        }

        /// <summary>
        /// Decodes the specified UTF-8 byte array to a string.
        /// </summary>
        /// <param name="bytes">The byte array to decode.</param>
        /// <returns>The decoded string.</returns>
        public static string FromBytes(this byte[] bytes) 
            => Encoding.UTF8.GetString(bytes);
    }
}
