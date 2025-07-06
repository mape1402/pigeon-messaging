namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Rabbit;
    using Pigeon.Messaging.Rabbit.Consuming;
    using Pigeon.Messaging.Rabbit.Producing;
    using RabbitMQ.Client;

    /// <summary>
    /// Extension methods to add RabbitMQ support to the Pigeon messaging infrastructure.
    /// </summary>
    public static class ServicesExtensions
    {
        /// <summary>
        /// Adds the RabbitMQ adapters and connection provider,
        /// binding optional custom configuration using an <see cref="Action{T}"/>.
        /// </summary>
        /// <param name="builder">The global settings builder.</param>
        /// <param name="config">An optional action to customize RabbitMQ settings.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> for chaining.</returns>
        public static GlobalSettingsBuilder UseRabbitMq(this GlobalSettingsBuilder builder, Action<RabbitSettings> config)
        {
            builder.AddFeature(ftBuilder =>
            {
                ftBuilder.Services.AddSingleton<IConnectionFactory>(_ => new ConnectionFactory());
                ftBuilder.Services.AddSingleton<IConnectionProvider, ConnectionProvider>();
                ftBuilder.Services.AddSingleton<IMessageBrokerConsumingAdapter, RabbitConsumingAdapter>();
                ftBuilder.Services.AddSingleton<IMessageBrokerProducingAdapter, RabbitProducingAdapter>();

                if (!ftBuilder.TryGetAdapterSettings<RabbitSettings>("RabbitMq", out var settings))
                    settings = new RabbitSettings();

                config(settings);

                ftBuilder.Services.AddSingleton(Options.Create(settings));
            });

            return builder;
        }

        /// <summary>
        /// Adds RabbitMQ with default settings.
        /// </summary>
        /// <param name="builder">The global settings builder.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> for chaining.</returns>
        public static GlobalSettingsBuilder UseRabbitMq(this GlobalSettingsBuilder builder)
            => builder.UseRabbitMq(s => { });
    }
}
