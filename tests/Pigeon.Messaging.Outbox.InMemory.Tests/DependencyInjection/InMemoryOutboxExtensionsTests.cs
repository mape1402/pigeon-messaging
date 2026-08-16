namespace Pigeon.Messaging.Outbox.InMemory.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Mule;
    using Mule.InMemory;
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
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IOutboxDiagnostics));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMuleClient));
            Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IInMemoryMule));
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

        [Fact]
        public void UseInMemoryOutbox_Should_Map_Throughput_Settings_To_Mule()
        {
            var services = new ServiceCollection();
            var builder = CreateBuilder(services);

            builder.UseInMemoryOutbox(settings =>
            {
                settings.DispatchQueueCapacity = 100_000;
                settings.DispatchBatchSize = 500;
                settings.WorkerCount = 16;
                settings.MaxDegreeOfParallelism = 128;
                settings.MaxDrainBatchesPerCycle = 8;
                settings.MaxDrainActionsPerCycle = 10_000;
                settings.DrainUntilEmpty = true;
                settings.YieldBetweenDrainBatches = TimeSpan.FromMilliseconds(2);
                settings.Lanes["critical"] = new OutboxLaneSettings
                {
                    WorkerCount = 4,
                    MaxDegreeOfParallelism = 32,
                    DispatchBatchSize = 250,
                    MaxDrainBatchesPerCycle = 3,
                    MaxDrainActionsPerCycle = 2_000,
                    DrainUntilEmpty = true,
                    DispatchQueueCapacity = 10_000,
                    PollingInterval = TimeSpan.FromSeconds(2),
                    Priority = 10,
                    Weight = 5
                };
            });

            using var provider = services.BuildServiceProvider();
            var muleSettings = provider.GetRequiredService<IOptions<MuleSettings>>().Value;

            Assert.Equal(100_000, muleSettings.DispatchQueueCapacity);
            Assert.Equal(500, muleSettings.DispatchBatchSize);
            Assert.Equal(16, muleSettings.WorkerCount);
            Assert.Equal(128, muleSettings.MaxDegreeOfParallelism);
            Assert.Equal(8, muleSettings.MaxDrainBatchesPerCycle);
            Assert.Equal(10_000, muleSettings.MaxDrainActionsPerCycle);
            Assert.True(muleSettings.DrainUntilEmpty);
            Assert.Equal(TimeSpan.FromMilliseconds(2), muleSettings.YieldBetweenDrainBatches);
            Assert.True(muleSettings.Lanes.ContainsKey("critical"));
            Assert.Equal(4, muleSettings.Lanes["critical"].WorkerCount);
            Assert.Equal(32, muleSettings.Lanes["critical"].MaxDegreeOfParallelism);
            Assert.Equal(250, muleSettings.Lanes["critical"].DispatchBatchSize);
            Assert.Equal(3, muleSettings.Lanes["critical"].MaxDrainBatchesPerCycle);
            Assert.Equal(2_000, muleSettings.Lanes["critical"].MaxDrainActionsPerCycle);
            Assert.True(muleSettings.Lanes["critical"].DrainUntilEmpty);
            Assert.Equal(10_000, muleSettings.Lanes["critical"].DispatchQueueCapacity);
            Assert.Equal(TimeSpan.FromSeconds(2), muleSettings.Lanes["critical"].PollingInterval);
            Assert.Equal(10, muleSettings.Lanes["critical"].Priority);
            Assert.Equal(5, muleSettings.Lanes["critical"].Weight);
        }

        [Fact]
        public void ConfigureHighThroughput_Should_Set_Productive_Outbox_Defaults()
        {
            var settings = new OutboxSettings();

            var result = settings.ConfigureHighThroughput();

            Assert.Same(settings, result);
            Assert.True(settings.ImmediateDispatch);
            Assert.Equal(Environment.ProcessorCount, settings.WorkerCount);
            Assert.Equal(Environment.ProcessorCount * 8, settings.MaxDegreeOfParallelism);
            Assert.True(settings.DispatchBatchSize >= 250);
            Assert.True(settings.MaxDrainBatchesPerCycle >= 8);
            Assert.True(settings.MaxDrainActionsPerCycle >= 2_000);
            Assert.Equal(TimeSpan.FromMilliseconds(1), settings.YieldBetweenDrainBatches);
            Assert.True(settings.DispatchInterval <= TimeSpan.FromSeconds(5));
        }

        private static GlobalSettingsBuilder CreateBuilder(IServiceCollection services)
            => new(
                services,
                new ConfigurationBuilder().Build(),
                Substitute.For<IConsumingConfigurator>(),
                new MessagingSettings { MessageBrokers = new() });
    }
}
