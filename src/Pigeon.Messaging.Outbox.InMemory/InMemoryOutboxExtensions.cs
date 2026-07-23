namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Outbox.InMemory;

    /// <summary>
    /// Provides in-memory outbox registration helpers.
    /// </summary>
    public static class InMemoryOutboxExtensions
    {
        /// <summary>
        /// Enables the Pigeon transactional outbox using process-local in-memory storage.
        /// </summary>
        /// <param name="builder">The Pigeon global settings builder.</param>
        /// <param name="configure">An optional callback to configure outbox behavior.</param>
        /// <returns>The same global settings builder for chaining.</returns>
        public static GlobalSettingsBuilder UseInMemoryOutbox(
            this GlobalSettingsBuilder builder,
            Action<OutboxSettings> configure = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.ConfigureOutbox(settings =>
            {
                settings.Enabled = true;
                settings.SchemaMode = OutboxSchemaMode.Manual;
                configure?.Invoke(settings);
            });

            builder.AddFeature(feature =>
            {
                feature.Services.AddSingleton<InMemoryOutboxStore>();
                feature.Services.AddSingleton<IInMemoryOutbox>(provider => provider.GetRequiredService<InMemoryOutboxStore>());
                feature.Services.AddScoped<IOutboxStorage, InMemoryOutboxStorage>();
                feature.Services.AddScoped<IOutboxDiagnostics, InMemoryOutboxDiagnostics>();
                feature.Services.AddSingleton<IOutboxSchemaInitializer, InMemoryOutboxSchemaInitializer>();
            });

            return builder;
        }
    }
}
