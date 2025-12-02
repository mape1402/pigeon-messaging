namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Azure.EventHub;
    using Pigeon.Messaging.Azure.EventHub.Consuming;
    using Pigeon.Messaging.Azure.EventHub.Producing;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing.Management;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Extension methods for registering Azure Event Hub messaging services and adapters in the DI container.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers Azure Event Hub messaging services, adapters, and configuration in the DI container using the provided configuration action.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <param name="config">An action to configure <see cref="AzureEventHubSettings"/>.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseAzureEventHub(this GlobalSettingsBuilder builder, Action<AzureEventHubSettings> config)
        {
            builder.AddFeature(ftBuilder =>
            {
                ftBuilder.Services.AddSingleton<IEventHubProvider, EventHubProvider>();
                ftBuilder.Services.AddSingleton<IMessageBrokerProducingAdapter, EventHubProducingAdapter>();
                ftBuilder.Services.AddSingleton<IMessageBrokerConsumingAdapter, EventHubConsumingAdapter>();

                if (!ftBuilder.TryGetAdapterSettings<AzureEventHubSettings>("AzureEventHub", out var settings))
                    settings = new AzureEventHubSettings();

                config(settings);

                ftBuilder.Services.AddSingleton(Options.Create(settings));
            });

            return builder;
        }

        /// <summary>
        /// Registers Azure Event Hub messaging services and adapters in the DI container with default settings.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseAzureEventHub(this GlobalSettingsBuilder builder)
            => builder.UseAzureEventHub(s => { });
    }
}