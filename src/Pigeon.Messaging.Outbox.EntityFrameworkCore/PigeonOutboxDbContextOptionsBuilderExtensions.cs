namespace Microsoft.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Pigeon.Messaging.Outbox.EntityFrameworkCore.Internal;

    internal static class PigeonOutboxDbContextOptionsBuilderExtensions
    {
        public static DbContextOptionsBuilder UsePigeonOutboxModel(this DbContextOptionsBuilder optionsBuilder)
        {
            var extension = optionsBuilder.Options.FindExtension<PigeonOutboxOptionsExtension>()
                ?? new PigeonOutboxOptionsExtension();

            ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(extension);
            return optionsBuilder;
        }
    }
}
