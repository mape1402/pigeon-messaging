namespace Pigeon.Messaging.Outbox.InMemory.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Outbox.InMemory;

    public class InMemoryOutboxExtensionsTests
    {
        [Fact]
        public void UseInMemoryOutbox_Should_Register_Outbox_Services_And_Enable_Outbox()
        {
            var services = new ServiceCollection();
            var builder = CreateBuilder(services);

            var result = builder.UseInMemoryOutbox();

            Assert.Same(builder, result);
            Assert.True(builder.GlobalSettings.Outbox.Enabled);
            Assert.Equal(OutboxSchemaMode.Manual, builder.GlobalSettings.Outbox.SchemaMode);
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IInMemoryOutbox));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOutboxStorage));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOutboxDiagnostics));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOutboxSchemaInitializer));
        }

        [Fact]
        public void UseInMemoryOutbox_Should_Apply_Custom_Settings()
        {
            var services = new ServiceCollection();
            var builder = CreateBuilder(services);

            builder.UseInMemoryOutbox(settings =>
            {
                settings.ImmediateDispatch = false;
                settings.DispatchBatchSize = 7;
            });

            Assert.False(builder.GlobalSettings.Outbox.ImmediateDispatch);
            Assert.Equal(7, builder.GlobalSettings.Outbox.DispatchBatchSize);
        }

        private static GlobalSettingsBuilder CreateBuilder(IServiceCollection services)
            => new(
                services,
                new ConfigurationBuilder().Build(),
                Substitute.For<IConsumingConfigurator>(),
                new MessagingSettings { MessageBrokers = new() });
    }
}
