namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System.Reflection;

    /// <summary>
    /// Provides a fluent builder for configuring global messaging settings,
    /// scanning consumers, adding features, and registering services
    /// for the Pigeon messaging infrastructure.
    /// </summary>
    public class GlobalSettingsBuilder
    {
        private readonly IServiceCollection _services;
        private readonly IConfiguration _configuration;
        private readonly IConsumingConfigurator _consumingConfigurator;
        private readonly MessagingSettings _messagingSettings;

        private readonly FeatureBuilder _featureBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="GlobalSettingsBuilder"/> class.
        /// </summary>
        /// <param name="services">The DI service collection.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="consumingConfigurator">The consuming configurator for managing consumer registrations.</param>
        /// <param name="messagingSettings">The strongly-typed messaging settings.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of the required dependencies are null.
        /// </exception>
        public GlobalSettingsBuilder(
            IServiceCollection services,
            IConfiguration configuration,
            IConsumingConfigurator consumingConfigurator,
            MessagingSettings messagingSettings)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _consumingConfigurator = consumingConfigurator ?? throw new ArgumentNullException(nameof(consumingConfigurator));
            _messagingSettings = messagingSettings ?? throw new ArgumentNullException(nameof(messagingSettings));

            _featureBuilder = new FeatureBuilder
            {
                Configuration = _configuration,
                MessagingSettings = _messagingSettings,
                Services = _services
            };

            GlobalSettings = new GlobalSettings
            {
                Domain = messagingSettings.Domain
            };

            services.AddSingleton(GlobalSettings);
        }

        /// <summary>
        /// Gets the global settings for Pigeon, such as domain and assemblies to scan.
        /// </summary>
        public GlobalSettings GlobalSettings { get; }

        /// <summary>
        /// Overrides the default domain for published messages.
        /// </summary>
        /// <param name="domain">The new domain name.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public GlobalSettingsBuilder SetDomain(string domain)
        {
            GlobalSettings.Domain = domain;
            return this;
        }

        /// <summary>
        /// Specifies which assemblies should be scanned for consumers
        /// decorated with the <c>ConsumerAttribute</c>.
        /// </summary>
        /// <param name="assemblies">The assemblies to scan.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public GlobalSettingsBuilder ScanConsumersFromAssemblies(params Assembly[] assemblies)
        {
            GlobalSettings.TargetAssemblies = assemblies;
            return this;
        }

        /// <summary>
        /// Adds a service and its implementation to the DI container.
        /// </summary>
        /// <typeparam name="TService">The service interface type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="lifetime">The lifetime of the service (e.g., Scoped, Singleton, Transient).</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public GlobalSettingsBuilder AddService<TService, TImplementation>(ServiceLifetime lifetime)
            where TImplementation : class, TService
        {
            _services.Add(new ServiceDescriptor(typeof(TService), typeof(TImplementation), lifetime));
            return this;
        }

        /// <summary>
        /// Adds a service and its implementation to the DI container
        /// using the specified types.
        /// </summary>
        /// <param name="serviceType">The service interface type.</param>
        /// <param name="implementationType">The implementation type.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the implementation type does not implement the service type.
        /// </exception>
        public GlobalSettingsBuilder AddService(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            if (!serviceType.IsAssignableFrom(implementationType))
                throw new InvalidOperationException(
                    $"Implementation type '{implementationType.Name}' doesn't implement service type '{serviceType.Name}'.");

            _services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
            return this;
        }

        /// <summary>
        /// Adds a keyed service and its implementation to the DI container.
        /// This is useful when multiple implementations of the same interface are required.
        /// </summary>
        /// <typeparam name="TService">The service interface type.</typeparam>
        /// <typeparam name="TImplementation">The implementation type.</typeparam>
        /// <param name="key">A key used to resolve the implementation.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public GlobalSettingsBuilder AddKeyedService<TService, TImplementation>(object key, ServiceLifetime lifetime)
            where TImplementation : class, TService
        {
            _services.Add(new ServiceDescriptor(typeof(TService), key, typeof(TImplementation), lifetime));
            return this;
        }

        /// <summary>
        /// Adds a keyed service and its implementation to the DI container.
        /// </summary>
        /// <param name="key">A key used to resolve the implementation.</param>
        /// <param name="serviceType">The service interface type.</param>
        /// <param name="implementationType">The implementation type.</param>
        /// <param name="lifetime">The service lifetime.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the implementation type does not implement the service type.
        /// </exception>
        public GlobalSettingsBuilder AddKeyedService(object key, Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            if (!serviceType.IsAssignableFrom(implementationType))
                throw new InvalidOperationException(
                    $"Implementation type '{implementationType.Name}' doesn't implement service type '{serviceType.Name}'.");

            _services.Add(new ServiceDescriptor(serviceType, key, implementationType, lifetime));
            return this;
        }

        /// <summary>
        /// Configures additional features by invoking the provided configuration action.
        /// </summary>
        /// <param name="config">
        /// The configuration action that receives the <see cref="FeatureBuilder"/>
        /// to register broker adapters, custom settings, or related services.
        /// </param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public GlobalSettingsBuilder AddFeature(Action<FeatureBuilder> config)
        {
            config(_featureBuilder);
            return this;
        }

        /// <summary>
        /// Registers a new consumer handler for a topic and version.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="topic">The topic name.</param>
        /// <param name="version">The semantic version of the message.</param>
        /// <param name="handler">The handler delegate to invoke when a message is consumed.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        internal GlobalSettingsBuilder AddConsumeHandler<T>(string topic, SemanticVersion version, ConsumeHandler<T> handler)
            where T : class
        {
            _consumingConfigurator.AddConsumer(topic, version, handler);
            return this;
        }

        /// <summary>
        /// Registers a new consumer handler for a topic using the default version.
        /// </summary>
        /// <typeparam name="T">The message type.</typeparam>
        /// <param name="topic">The topic name.</param>
        /// <param name="handler">The handler delegate to invoke when a message is consumed.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        internal GlobalSettingsBuilder AddConsumeHandler<T>(string topic, ConsumeHandler<T> handler)
            where T : class
        {
            _consumingConfigurator.AddConsumer(topic, handler);
            return this;
        }
    }
}
