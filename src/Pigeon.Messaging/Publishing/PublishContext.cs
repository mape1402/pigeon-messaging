namespace Pigeon.Messaging.Publishing
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Context object passed to publish interceptors, allowing
    /// addition of metadata to enrich or modify the message before publishing.
    /// </summary>
    public class PublishContext
    {
        private readonly Dictionary<string, object> _metadata = new();

        /// <summary>
        /// Adds a metadata entry with the specified key and value.
        /// Throws <see cref="InvalidOperationException"/> if the key already exists.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value.</typeparam>
        /// <param name="key">The unique key identifying the metadata.</param>
        /// <param name="value">The metadata value to add.</param>
        /// <exception cref="InvalidOperationException">Thrown if the key already exists.</exception>
        public void AddMetadata<T>(string key, T value)
        {
            if(_metadata.ContainsKey(key))
                throw new InvalidOperationException($"Metadata with key '{key}' already exists.");

            _metadata.Add(key, value);
        }

        /// <summary>
        /// Retrieves a read-only view of the current metadata dictionary.
        /// This prevents external code from modifying the internal metadata directly.
        /// </summary>
        /// <returns>
        /// An <see cref="IReadOnlyDictionary{String, Object}"/> containing
        /// the metadata key-value pairs.
        /// </returns>
        internal IReadOnlyDictionary<string, object> GetMetadata()
            => new ReadOnlyDictionary<string, object>(_metadata);

    }
}
