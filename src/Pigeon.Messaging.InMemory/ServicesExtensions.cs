namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Topology;

    /// <summary>
    /// Extension methods to add in-memory broker support to Pigeon.
    /// </summary>
    public static class ServicesExtensions
    {
        /// <summary>
        /// Adds the in-memory broker adapters.
        /// </summary>
        /// <param name="builder">The global settings builder.</param>
        /// <returns>The same <see cref="GlobalSettingsBuilder"/> for chaining.</returns>
        public static GlobalSettingsBuilder UseInMemoryBroker(this GlobalSettingsBuilder builder)
        {
            builder.AddFeature(feature =>
            {
                feature.Services.AddSingleton<InMemoryMessageBroker>();
                feature.Services.AddSingleton<IInMemoryBroker>(provider => provider.GetRequiredService<InMemoryMessageBroker>());
                feature.Services.AddSingleton<IMessageBrokerConsumingAdapter, InMemoryConsumingAdapter>();
                feature.Services.AddSingleton<IMessageBrokerProducingAdapter, InMemoryProducingAdapter>();
                feature.Services.AddSingleton<IMessageBrokerTopologyAdapter, InMemoryTopologyAdapter>();
            });

            return builder;
        }
    }
}
