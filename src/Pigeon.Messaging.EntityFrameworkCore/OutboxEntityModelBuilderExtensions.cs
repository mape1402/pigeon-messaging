namespace Microsoft.EntityFrameworkCore
{
    using Pigeon.Messaging.Outbox;

    /// <summary>
    /// Provides EF Core model configuration for Pigeon outbox entities.
    /// </summary>
    public static class OutboxEntityModelBuilderExtensions
    {
        public static ModelBuilder AddPigeonOutbox(this ModelBuilder modelBuilder)
        {
            var entity = modelBuilder.Entity<OutboxMessage>();

            entity.ToTable("PigeonOutboxMessages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Payload).IsRequired();
            entity.Property(x => x.PayloadType).IsRequired().HasMaxLength(1024);
            entity.Property(x => x.Topic).HasMaxLength(512);
            entity.Property(x => x.Exchange).HasMaxLength(512);
            entity.Property(x => x.RoutingKey).HasMaxLength(512);
            entity.Property(x => x.LastError);
            entity.HasIndex(x => new { x.Status, x.NextAttemptOnUtc });
            entity.HasIndex(x => x.PublishedOnUtc);

            return modelBuilder;
        }
    }
}
