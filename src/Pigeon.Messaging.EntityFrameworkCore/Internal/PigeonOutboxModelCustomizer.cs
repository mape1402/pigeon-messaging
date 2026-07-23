namespace Pigeon.Messaging.EntityFrameworkCore.Internal
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;

    internal sealed class PigeonOutboxModelCustomizer : ModelCustomizer
    {
        public PigeonOutboxModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            modelBuilder.AddPigeonOutbox();
        }
    }
}
