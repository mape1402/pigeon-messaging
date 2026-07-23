namespace Pigeon.Messaging.Outbox.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore;

    internal interface IOutboxDbContextFactory<TDbContext>
        where TDbContext : DbContext
    {
        TDbContext CreateDbContext();
    }
}
