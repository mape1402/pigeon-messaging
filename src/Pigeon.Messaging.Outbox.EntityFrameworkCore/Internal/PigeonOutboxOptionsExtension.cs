namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Internal
{
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    internal sealed class PigeonOutboxOptionsExtension : IDbContextOptionsExtension
    {
        private DbContextOptionsExtensionInfo _info;

        public DbContextOptionsExtensionInfo Info => _info ??= new ExtensionInfo(this);

        public void ApplyServices(IServiceCollection services)
        {
            services.Replace(ServiceDescriptor.Singleton<IModelCustomizer, PigeonOutboxModelCustomizer>());
        }

        public void Validate(IDbContextOptions options)
        {
        }

        private sealed class ExtensionInfo : DbContextOptionsExtensionInfo
        {
            public ExtensionInfo(IDbContextOptionsExtension extension)
                : base(extension)
            {
            }

            public override bool IsDatabaseProvider => false;

            public override string LogFragment => "using PigeonOutbox ";

            public override int GetServiceProviderHashCode()
                => typeof(PigeonOutboxOptionsExtension).GetHashCode();

            public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                => debugInfo["PigeonOutbox"] = "1";

            public override bool ShouldUseSameServiceProvider(DbContextOptionsExtensionInfo other)
                => other is ExtensionInfo;
        }
    }
}
