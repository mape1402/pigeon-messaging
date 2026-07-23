namespace Pigeon.Messaging.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    internal sealed class OutboxDbContextFactory<TDbContext> : IOutboxDbContextFactory<TDbContext>
        where TDbContext : DbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly DbContextOptions<TDbContext> _options;

        public OutboxDbContextFactory(IServiceProvider serviceProvider, DbContextOptions<TDbContext> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public TDbContext CreateDbContext()
            => ActivatorUtilities.CreateInstance<TDbContext>(_serviceProvider, _options);
    }
}
