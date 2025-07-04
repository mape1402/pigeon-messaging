namespace Pigeon.Messaging.Producing
{
    using System.Collections.Concurrent;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Context object passed to publish interceptors, allowing
    /// addition of metadata to enrich or modify the message before publishing.
    /// </summary>
    public class PublishContext
    {
        private readonly ConcurrentDictionary<string, object> _metadata = new();

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
            if(string.IsNullOrWhiteSpace(key)) 
                throw new ArgumentNullException(nameof(key));

            if(!_metadata.TryAdd(key, value))
                throw new InvalidOperationException($"RawMetadata with key '{key}' already exists.");
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
