namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Topology;
    using Pigeon.Testing;
    using System.Reflection;

    /// <summary>
    /// Registers Pigeon testing services.
    /// </summary>
    public static class PigeonTestingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers an in-memory Pigeon testing transport.
        /// </summary>
        public static IServiceCollection AddPigeonTesting(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (services.Any(descriptor => descriptor.ServiceType == typeof(IPigeonTestingTransport)))
                return services;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Pigeon.Testing"
                })
                .Build();

            services.AddPigeon(configuration, config =>
            {
                config.SetDomain("Pigeon.Testing")
                    .AddFeature(feature =>
                    {
                        feature.Services.AddSingleton<PigeonTestingTransport>();
                        feature.Services.AddSingleton<IPigeonTestingTransport>(provider => provider.GetRequiredService<PigeonTestingTransport>());
                        feature.Services.AddSingleton<IMessageBrokerProducingAdapter, PigeonTestingProducingAdapter>();
                        feature.Services.AddSingleton<IMessageBrokerConsumingAdapter, PigeonTestingConsumingAdapter>();
                        feature.Services.AddSingleton<IMessageBrokerTopologyAdapter, PigeonTestingTopologyAdapter>();
                    });
            });

            return services;
        }

        /// <summary>
        /// Registers Pigeon testing consumers from one or more assemblies.
        /// </summary>
        public static IServiceCollection AddPigeonTestingConsumers(this IServiceCollection services, params Assembly[] assemblies)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            services.AddPigeonTesting();

            var consumingConfigurator = services
                .FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IConsumingConfigurator))
                ?.ImplementationInstance as IConsumingConfigurator;

            if (consumingConfigurator == null)
                throw new InvalidOperationException("Pigeon testing could not resolve the consuming configurator.");

            new PigeonTestingConsumerScanner(services, consumingConfigurator).Scan(assemblies);
            return services;
        }

        /// <summary>
        /// Registers the testing adapter surface for external test hosts.
        /// </summary>
        public static IServiceCollection AddPigeonTestingAdapter(this IServiceCollection services, params Assembly[] assemblies)
            => services.AddPigeonTesting().AddPigeonTestingConsumers(assemblies);
    }
}
