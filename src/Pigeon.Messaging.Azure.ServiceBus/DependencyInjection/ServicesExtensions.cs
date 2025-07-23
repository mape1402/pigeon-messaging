namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging.Azure.ServiceBus;
    using Pigeon.Messaging.Azure.ServiceBus.Consuming;
    using Pigeon.Messaging.Azure.ServiceBus.Producing;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing.Management;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Extension methods for registering Azure Service Bus messaging services and adapters in the DI container.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers Azure Service Bus messaging services, adapters, and configuration in the DI container using the provided configuration action.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <param name="config">An action to configure <see cref="AzureServiceBusSettings"/>.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseAzureServiceBus(this GlobalSettingsBuilder builder, Action<AzureServiceBusSettings> config)
        {
            builder.AddFeature(ftBuilder =>
            {
                ftBuilder.Services.AddSingleton<IServiceBusProvider, ServiceBusProvider>();
                ftBuilder.Services.AddSingleton<IMessageBrokerProducingAdapter, ServiceBusProducingAdapter>();
                ftBuilder.Services.AddSingleton<IMessageBrokerConsumingAdapter, ServiceBusConsumingAdapter>();

                if(!ftBuilder.TryGetAdapterSettings<AzureServiceBusSettings>("AzureServiceBus", out var settings))
                    settings = new AzureServiceBusSettings();

                config(settings);

                ftBuilder.Services.AddSingleton(Options.Options.Create(settings));
            });

            return builder;
        }

        /// <summary>
        /// Registers Azure Service Bus messaging services and adapters in the DI container with default settings.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseAzureServiceBus(this GlobalSettingsBuilder builder)
            => builder.UseAzureServiceBus(s => { });
    }
}
