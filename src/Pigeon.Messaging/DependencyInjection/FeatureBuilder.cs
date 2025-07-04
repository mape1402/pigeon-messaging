namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Pigeon.Messaging;

    /// <summary>
    /// Provides contextual services and configuration for adding custom features
    /// to the Pigeon messaging infrastructure.
    /// </summary>
    /// <remarks>
    /// This builder is typically used inside a feature registration delegate
    /// to access the DI container, app configuration, and global messaging settings.
    /// </remarks>
    public class FeatureBuilder
    {
        /// <summary>
        /// Gets the <see cref="IServiceCollection"/> used to register
        /// services related to the current messaging feature.
        /// </summary>
        /// <remarks>
        /// Use this to add adapters, brokers, or any supporting services.
        /// </remarks>
        public IServiceCollection Services { get; init; }

        /// <summary>
        /// Gets the application-wide <see cref="IConfiguration"/> instance.
        /// </summary>
        /// <remarks>
        /// Use this to bind custom broker settings or other feature-specific configuration sections.
        /// </remarks>
        public IConfiguration Configuration { get; init; }

        /// <summary>
        /// Gets the strongly-typed global messaging settings for the Pigeon framework.
        /// </summary>
        /// <remarks>
        /// This allows feature implementations to access the domain or broker-specific
        /// configuration if needed.
        /// </remarks>
        public MessagingSettings MessagingSettings { get; init; }

        /// <summary>
        /// Attempts to retrieve a strongly-typed settings object for a specific adapter
        /// from the configured <see cref="MessagingSettings"/>.
        /// </summary>
        /// <typeparam name="T">The type of the settings object to bind to.</typeparam>
        /// <param name="adapterKey">The key name of the message broker or adapter in the configuration.</param>
        /// <param name="output">
        /// When this method returns, contains the bound settings instance if found; otherwise, the specified default value.
        /// </param>
        /// <returns><c>true</c> if the settings were found and bound successfully; otherwise, <c>false</c>.</returns>

        public bool TryGetAdapterSettings<T>(string adapterKey, out T output)
        {
            output = default;

            if(!MessagingSettings.MessageBrokers.TryGetValue(adapterKey, out var section))
                return false;

            output = section.Get<T>();
            return true;
        }
    }
}
