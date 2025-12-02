namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Azure.EventGrid;
    using Pigeon.Messaging.Azure.EventGrid.Consuming;
    using Pigeon.Messaging.Azure.EventGrid.Producing;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing.Management;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Extension methods for registering Azure Event Grid messaging services and adapters in the DI container.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers Azure Event Grid messaging services, adapters, and configuration in the DI container using the provided configuration action.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <param name="config">An action to configure <see cref="AzureEventGridSettings"/>.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseAzureEventGrid(this GlobalSettingsBuilder builder, Action<AzureEventGridSettings> config)
        {
            builder.AddFeature(ftBuilder =>
            {
                ftBuilder.Services.AddSingleton<IEventGridProvider, EventGridProvider>();
                ftBuilder.Services.AddSingleton<IMessageBrokerProducingAdapter, EventGridProducingAdapter>();
                ftBuilder.Services.AddSingleton<IMessageBrokerConsumingAdapter, EventGridConsumingAdapter>();

                if (!ftBuilder.TryGetAdapterSettings<AzureEventGridSettings>("AzureEventGrid", out var settings))
                    settings = new AzureEventGridSettings();

                config(settings);

                ftBuilder.Services.AddSingleton(Options.Create(settings));
            });

            return builder;
        }

        /// <summary>
        /// Registers Azure Event Grid messaging services and adapters in the DI container with default settings.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseAzureEventGrid(this GlobalSettingsBuilder builder)
            => builder.UseAzureEventGrid(s => { });
    }
}