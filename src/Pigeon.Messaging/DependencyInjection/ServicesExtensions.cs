namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Provides extension methods for registering the Pigeon messaging infrastructure
    /// into the <see cref="IServiceCollection"/>.
    /// </summary>
    public static class ServicesExtensions
    {
        private const string PigeonSettingsKeyMap = "Pigeon";

        /// <summary>
        /// Registers core Pigeon services, configuration, and consumers.
        /// This is the main entry point for enabling the messaging framework.
        /// </summary>
        /// <param name="services">The DI service collection to configure.</param>
        /// <param name="configuration">The root application configuration.</param>
        /// <param name="config">
        /// An action that allows further configuration of global settings,
        /// consuming handlers, features, or adapters.
        /// </param>
        /// <returns>An <see cref="IPigeonServiceBuilder"/> for fluent configuration.</returns>
        public static IPigeonServiceBuilder AddPigeon(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<GlobalSettingsBuilder> config)
        {
            // Register core consuming components.
            IConsumingConfigurator consumingConfigurator = new ConsumingConfigurator();
            services.AddSingleton(consumingConfigurator);
            services.AddSingleton<IConsumingDispatcher, ConsumingDispatcher>();
            services.AddSingleton<IConsumingManager, ConsumingManager>();

            // Bind the MessagingSettings from configuration.
            var settings = configuration
                .GetSection(PigeonSettingsKeyMap)
                .Get<MessagingSettings>();

            services.Configure<MessagingSettings>(
                opts => configuration.GetSection(PigeonSettingsKeyMap).Bind(opts));

            // Initialize the global settings builder.
            var settingsBuilder = new GlobalSettingsBuilder(
                services, configuration, consumingConfigurator, settings);

            // Apply user configuration via the callback.
            config(settingsBuilder);

            // Scan assemblies for consumers and register them.
            new ConsumerScanner(services, consumingConfigurator)
                .Scan(settingsBuilder.GlobalSettings.TargetAssemblies);

            return new PigeonServiceBuilder(settingsBuilder);
        }

        /// <summary>
        /// Registers a custom consume interceptor that can run logic before or after
        /// dispatching messages to consumers.
        /// </summary>
        /// <typeparam name="TInterceptor">
        /// The interceptor type implementing <see cref="IConsumeInterceptor"/>.
        /// </typeparam>
        /// <param name="builder">The Pigeon service builder.</param>
        /// <returns>The same <see cref="IPigeonServiceBuilder"/> instance for chaining.</returns>
        public static IPigeonServiceBuilder AddConsumeInterceptor<TInterceptor>(
            this IPigeonServiceBuilder builder)
            where TInterceptor : class, IConsumeInterceptor
        {
            builder.GlobalSettingsBuilder.AddService<IConsumeInterceptor, TInterceptor>(ServiceLifetime.Scoped);
            return builder;
        }

        /// <summary>
        /// Registers a custom publish interceptor that can run logic before or after
        /// publishing messages to the message broker.
        /// </summary>
        /// <typeparam name="TInterceptor">
        /// The interceptor type implementing <see cref="IPublishInterceptor"/>.
        /// </typeparam>
        /// <param name="builder">The Pigeon service builder.</param>
        /// <returns>The same <see cref="IPigeonServiceBuilder"/> instance for chaining.</returns>
        public static IPigeonServiceBuilder AddPublishInterceptor<TInterceptor>(
            this IPigeonServiceBuilder builder)
            where TInterceptor : class, IPublishInterceptor
        {
            builder.GlobalSettingsBuilder.AddService<IPublishInterceptor, TInterceptor>(ServiceLifetime.Scoped);
            return builder;
        }

        /// <summary>
        /// Registers a custom consume handler for a specific topic and version.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message that the handler consumes.
        /// </typeparam>
        /// <param name="builder">The Pigeon service builder.</param>
        /// <param name="topic">The topic name that the handler should listen to.</param>
        /// <param name="version">The semantic version of the message contract.</param>
        /// <param name="handler">The delegate that handles incoming messages.</param>
        /// <returns>The same <see cref="IPigeonServiceBuilder"/> instance for chaining.</returns>
        public static IPigeonServiceBuilder AddConsumeHandler<T>(
            this IPigeonServiceBuilder builder,
            string topic,
            SemanticVersion version,
            ConsumeHandler<T> handler)
            where T : class
        {
            builder.GlobalSettingsBuilder.AddConsumeHandler(topic, version, handler);
            return builder;
        }

        /// <summary>
        /// Registers a custom consume handler for a specific topic
        /// using the default semantic version.
        /// </summary>
        /// <typeparam name="T">
        /// The type of message that the handler consumes.
        /// </typeparam>
        /// <param name="builder">The Pigeon service builder.</param>
        /// <param name="topic">The topic name that the handler should listen to.</param>
        /// <param name="handler">The delegate that handles incoming messages.</param>
        /// <returns>The same <see cref="IPigeonServiceBuilder"/> instance for chaining.</returns>
        public static IPigeonServiceBuilder AddConsumeHandler<T>(
            this IPigeonServiceBuilder builder,
            string topic,
            ConsumeHandler<T> handler)
            where T : class
        {
            builder.GlobalSettingsBuilder.AddConsumeHandler(topic, handler);
            return builder;
        }
    }
}
