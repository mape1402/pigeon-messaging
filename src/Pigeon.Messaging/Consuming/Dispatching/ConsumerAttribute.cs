namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Specifies that a method is a message consumer handler for a given topic and optional version.
    /// Methods marked with this attribute will be discovered and registered as message handlers.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class ConsumerAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerAttribute"/> class
        /// with the specified topic and the default semantic version ("1.0.0").
        /// </summary>
        /// <param name="topic">The topic name to subscribe the consumer method to.</param>
        public ConsumerAttribute(string topic) : this(topic, SemanticVersion.Default) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumerAttribute"/> class
        /// with the specified topic and semantic version.
        /// </summary>
        /// <param name="topic">The topic name to subscribe the consumer method to.</param>
        /// <param name="version">The semantic version of the message contract.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="topic"/> is null or empty.</exception>
        /// <exception cref="FormatException">Thrown if <paramref name="version"/> is not a valid semantic version string.</exception>
        public ConsumerAttribute(string topic, string version)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentNullException(nameof(topic));

            if (!SemanticVersion.TryParse(version, out _))
                throw new FormatException("Invalid Semantic Version format.");

            Topic = topic;
            Version = version;
        }

        /// <summary>
        /// Gets the topic name this consumer method subscribes to.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the semantic version string of the message contract this consumer method handles.
        /// </summary>
        public string Version { get; }
    }
}
