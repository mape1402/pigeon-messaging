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
    }
}
