namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Pigeon.Messaging.EntityFrameworkCore;
    using Pigeon.Messaging.Outbox;

    /// <summary>
    /// Provides Entity Framework Core outbox registration helpers.
    /// </summary>
    public static class EntityFrameworkOutboxExtensions
    {
        public static GlobalSettingsBuilder UseEntityFrameworkOutbox<TDbContext>(
            this GlobalSettingsBuilder builder,
            Action<OutboxSettings> configure = null)
            where TDbContext : DbContext
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            builder.ConfigureOutbox(settings =>
            {
                settings.Enabled = true;
                configure?.Invoke(settings);
            });

            builder.AddFeature(feature =>
            {
                feature.Services.AddScoped<IOutboxDbContextFactory<TDbContext>, OutboxDbContextFactory<TDbContext>>();
                feature.Services.AddScoped<IOutboxStorage, EntityFrameworkOutboxStorage<TDbContext>>();
                feature.Services.AddScoped<IOutboxDiagnostics, EntityFrameworkOutboxDiagnostics<TDbContext>>();
                feature.Services.AddScoped<IOutboxSchemaInitializer, EntityFrameworkOutboxSchemaInitializer<TDbContext>>();
                feature.Services.AddPigeonOutboxDbContextOptions<TDbContext>();
            });

            return builder;
        }

        private static IServiceCollection AddPigeonOutboxDbContextOptions<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            var serviceType = typeof(DbContextOptions<TDbContext>);
            var descriptor = services.LastOrDefault(x => x.ServiceType == serviceType);

            if (descriptor == null)
                throw new InvalidOperationException($"DbContext '{typeof(TDbContext).Name}' must be registered before enabling the Pigeon EF outbox.");

            services.Remove(descriptor);
            services.Add(new ServiceDescriptor(serviceType, provider =>
            {
                var options = ResolveOptions<TDbContext>(descriptor, provider);
                return new DbContextOptionsBuilder<TDbContext>(options)
                    .UsePigeonOutboxModel()
                    .Options;
            }, descriptor.Lifetime));

            return services;
        }

        private static DbContextOptions<TDbContext> ResolveOptions<TDbContext>(ServiceDescriptor descriptor, IServiceProvider provider)
            where TDbContext : DbContext
        {
            if (descriptor.ImplementationInstance is DbContextOptions<TDbContext> instance)
                return instance;

            if (descriptor.ImplementationFactory != null)
                return (DbContextOptions<TDbContext>)descriptor.ImplementationFactory(provider);

            if (descriptor.ImplementationType != null)
                return (DbContextOptions<TDbContext>)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType);

            throw new InvalidOperationException($"Unable to resolve DbContextOptions for '{typeof(TDbContext).Name}'.");
        }
    }
}
