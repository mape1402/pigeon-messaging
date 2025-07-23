namespace Pigeon.Messaging.Kafka.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Kafka.Consuming;
    using Pigeon.Messaging.Kafka.Producing;
    using Pigeon.Messaging.Producing.Management;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Extension methods for registering Kafka messaging services and adapters in the DI container.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class ServicesExtensions
    {
        /// <summary>
        /// Registers Kafka messaging services, adapters, and configuration in the DI container using the provided configuration action.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <param name="config">An action to configure <see cref="KafkaSettings"/>.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseKafka(this GlobalSettingsBuilder builder, Action<KafkaSettings> config)
        {
            builder.AddFeature(ftBuilder =>
            {
                ftBuilder.Services.AddSingleton<IConfigurationProvider, ConfigurationProvider>();
                ftBuilder.Services.AddSingleton(typeof(IKafkaProducer<>), typeof(KafkaProducer<>));
                ftBuilder.Services.AddSingleton<IMessageBrokerConsumingAdapter, KafkaConsumingAdapter>();
                ftBuilder.Services.AddSingleton<IMessageBrokerProducingAdapter, KafkaProducingAdapter>();

                if (!ftBuilder.TryGetAdapterSettings<KafkaSettings>("Kafka", out var settings))
                    settings = new KafkaSettings();
                
                config(settings);
                
                ftBuilder.Services.AddSingleton(Options.Create(settings));
            });

            return builder;
        }

        /// <summary>
        /// Registers Kafka messaging services and adapters in the DI container with default settings.
        /// </summary>
        /// <param name="builder">The global settings builder for messaging configuration.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> instance for chaining.</returns>
        public static GlobalSettingsBuilder UseKafka(this GlobalSettingsBuilder builder)
            => builder.UseKafka(s => { });
    }
}
