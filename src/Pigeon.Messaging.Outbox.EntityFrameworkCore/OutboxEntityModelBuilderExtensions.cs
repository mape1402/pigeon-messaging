namespace Microsoft.EntityFrameworkCore
{
    using Mule.EntityFrameworkCore;

    /// <summary>
    /// Provides EF Core model configuration for Pigeon outbox entities.
    /// </summary>
    public static class OutboxEntityModelBuilderExtensions
    {
        /// <summary>
        /// Adds the Pigeon outbox entity mapping to an EF Core model.
        /// </summary>
        /// <param name="modelBuilder">The model builder to configure.</param>
        /// <returns>The same model builder for chaining.</returns>
        public static ModelBuilder AddPigeonOutbox(this ModelBuilder modelBuilder)
        {
            return modelBuilder.UseMuleModel();
        }
    }
}
