using Microsoft.EntityFrameworkCore;
using Pigeon.Messaging.Outbox;
using Mule.EntityFrameworkCore;

namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Tests;

internal sealed class LegacyOutboxDbContext(DbContextOptions<LegacyOutboxDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>().ToTable("PigeonOutboxMessages");
        modelBuilder.UseMuleModel();
    }
}
