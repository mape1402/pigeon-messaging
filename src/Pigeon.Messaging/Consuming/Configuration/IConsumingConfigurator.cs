namespace Pigeon.Messaging.Consuming.Configuration
{
    using Pigeon.Messaging.Contracts;

    /// <summary>
    /// Defines the contract for configuring and managing consumers
    /// for message topics and versions in the consuming engine.
    /// </summary>
    public interface IConsumingConfigurator
    {
        /// <summary>
        /// Registers a consumer handler for a specific topic and semantic version.
        /// </summary>
        /// <typeparam name="T">The type of the expected message payload.</typeparam>
        /// <param name="topic">The topic or channel to bind the consumer to.</param>
        /// <param name="version">The semantic version of the message contract.</param>
        /// <param name="handler">The delegate that handles the consumed message.</param>
        /// <returns>The same <see cref="IConsumingConfigurator"/> instance for fluent chaining.</returns>
        IConsumingConfigurator AddConsumer<T>(string topic, SemanticVersion version, ConsumeHandler<T> handler) where T : class;

        /// <summary>
        /// Registers a consumer handler for a specific topic
        /// using the default semantic version.
        /// </summary>
        /// <typeparam name="T">The type of the expected message payload.</typeparam>
        /// <param name="topic">The topic or channel to bind the consumer to.</param>
        /// <param name="handler">The delegate that handles the consumed message.</param>
        /// <returns>The same <see cref="IConsumingConfigurator"/> instance for fluent chaining.</returns>
        IConsumingConfigurator AddConsumer<T>(string topic, ConsumeHandler<T> handler) where T : class;

        /// <summary>
        /// Removes a consumer handler for a given topic and semantic version.
        /// </summary>
        /// <param name="topic">The topic or channel of the consumer to remove.</param>
        /// <param name="version">The semantic version of the message contract to remove.</param>
        /// <returns>The same <see cref="IConsumingConfigurator"/> instance for fluent chaining.</returns>
        IConsumingConfigurator RemoveConsumer(string topic, SemanticVersion version);

        /// <summary>
        /// Removes a consumer handler for a given topic,
        /// using the default semantic version.
        /// </summary>
        /// <param name="topic">The topic or channel of the consumers to remove.</param>
        /// <returns>The same <see cref="IConsumingConfigurator"/> instance for fluent chaining.</returns>
        IConsumingConfigurator RemoveConsumer(string topic);

        /// <summary>
        /// Retrieves the <see cref="ConsumerConfiguration"/> for a given topic and version.
        /// Throws an exception if no matching configuration is found.
        /// </summary>
        /// <param name="topic">The topic or channel to look up.</param>
        /// <param name="version">The semantic version of the message contract.</param>
        /// <returns>The matching <see cref="ConsumerConfiguration"/>.</returns>
        ConsumerConfiguration GetConfiguration(string topic, SemanticVersion version);

        /// <summary>
        /// Retrieves the <see cref="ConsumerConfiguration"/> for a given topic
        /// using the default semantic version.
        /// Throws an exception if no matching configuration is found.
        /// </summary>
        /// <param name="topic">The topic or channel to look up.</param>
        /// <returns>The matching <see cref="ConsumerConfiguration"/>.</returns>
        ConsumerConfiguration GetConfiguration(string topic);
    }
}
