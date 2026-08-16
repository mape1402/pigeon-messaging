namespace Pigeon.Messaging.Tests.DependencyInjection
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using Xunit;

    public class ServicesExtensionsTests
    {
        [Fact]
        public void AddPigeon_Should_Register_Core_Services()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });

            Assert.NotNull(builder);
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumingConfigurator));
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumingDispatcher));
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumingManager));
            Assert.Contains(services, d => d.ServiceType == typeof(IConsumerExecutionDiagnostics));
            Assert.Contains(services, d => d.ServiceType == typeof(IProducer));
            Assert.Contains(services, d => d.ServiceType == typeof(IProducingManager));
        }

        [Fact]
        public void AddPigeon_Should_Bind_Global_Settings_From_Configuration()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Configured",
                    ["Pigeon:ConsumerExecution:AcknowledgementMode"] = "OnHandlerSuccess",
                    ["Pigeon:ConsumerExecution:MaxConcurrency"] = "256",
                    ["Pigeon:ConsumerExecution:QueueCapacity"] = "10000",
                    ["Pigeon:ConsumerExecution:PrefetchCount"] = "512",
                    ["Pigeon:ConsumerExecution:HandlerTimeout"] = "00:02:00",
                    ["Pigeon:Outbox:Enabled"] = "true",
                    ["Pigeon:Outbox:ImmediateDispatch"] = "true",
                    ["Pigeon:Outbox:DispatchQueueCapacity"] = "100000",
                    ["Pigeon:Outbox:DispatchBatchSize"] = "500",
                    ["Pigeon:Outbox:WorkerCount"] = "16",
                    ["Pigeon:Outbox:MaxDegreeOfParallelism"] = "128",
                    ["Pigeon:Outbox:MaxDrainBatchesPerCycle"] = "8",
                    ["Pigeon:Outbox:MaxDrainActionsPerCycle"] = "10000",
                    ["Pigeon:Outbox:DrainUntilEmpty"] = "true",
                    ["Pigeon:Outbox:DispatchInterval"] = "00:00:05"
                })
                .Build();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });
            var settings = builder.GlobalSettingsBuilder.GlobalSettings;

            Assert.Equal("Configured", settings.Domain);
            Assert.Equal(MessageAcknowledgementMode.OnHandlerSuccess, settings.ConsumerExecution.AcknowledgementMode);
            Assert.Equal(256, settings.ConsumerExecution.MaxConcurrency);
            Assert.Equal(10000, settings.ConsumerExecution.QueueCapacity);
            Assert.Equal((ushort)512, settings.ConsumerExecution.PrefetchCount);
            Assert.Equal(TimeSpan.FromMinutes(2), settings.ConsumerExecution.HandlerTimeout);
            Assert.True(settings.Outbox.Enabled);
            Assert.True(settings.Outbox.ImmediateDispatch);
            Assert.Equal(100000, settings.Outbox.DispatchQueueCapacity);
            Assert.Equal(500, settings.Outbox.DispatchBatchSize);
            Assert.Equal(16, settings.Outbox.WorkerCount);
            Assert.Equal(128, settings.Outbox.MaxDegreeOfParallelism);
            Assert.Equal(8, settings.Outbox.MaxDrainBatchesPerCycle);
            Assert.Equal(10000, settings.Outbox.MaxDrainActionsPerCycle);
            Assert.True(settings.Outbox.DrainUntilEmpty);
            Assert.Equal(TimeSpan.FromSeconds(5), settings.Outbox.DispatchInterval);
        }

        [Fact]
        public void ConfigureHighThroughputConsumers_Should_Set_Bounded_Productive_Defaults()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder =>
            {
                settingsBuilder.ConfigureHighThroughputConsumers(concurrencyMultiplier: 2, queueCapacityMultiplier: 10, handlerTimeout: TimeSpan.FromMinutes(3));
            });

            var execution = builder.GlobalSettingsBuilder.GlobalSettings.ConsumerExecution;
            var expectedConcurrency = Environment.ProcessorCount * 2;

            Assert.Equal(expectedConcurrency, execution.MaxConcurrency);
            Assert.Equal(expectedConcurrency * 10, execution.QueueCapacity);
            Assert.Equal((ushort)Math.Min(ushort.MaxValue, expectedConcurrency), execution.PrefetchCount);
            Assert.Equal(TimeSpan.FromMinutes(3), execution.HandlerTimeout);
        }

        [Fact]
        public void AddConsumeInterceptor_Should_Add_ConsumeInterceptor()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });

            builder.AddConsumeInterceptor<SampleInterceptor>();

            // Verifies descriptor is added
            var found = services.Any(d => d.ServiceType == typeof(IConsumeInterceptor) && d.ImplementationType == typeof(SampleInterceptor));
            Assert.True(found);
        }

        [Fact]
        public void AddPublishInterceptor_Should_Add_PublishInterceptor()
        {
            var services = new ServiceCollection();
            var configuration = StubConfiguration();

            var builder = services.AddPigeon(configuration, settingsBuilder => { });

            builder.AddPublishInterceptor<SamplePubInterceptor>();

            var found = services.Any(d => d.ServiceType == typeof(IPublishInterceptor) && d.ImplementationType == typeof(SamplePubInterceptor));
            Assert.True(found);
        }

        private IConfiguration StubConfiguration()
        {
            var json = 
            @"
              {
                ""Pigeon"": {
                    ""Domain"": ""Tests"",
                    ""MessageBrokers"": {
                        ""TestingBroker"": {
                            ""Url"": ""testconnection""
                        }
                    }
                }
              }";

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(json);
            writer.Flush();
            stream.Position = 0;

            return new ConfigurationBuilder()
                        .AddJsonStream(stream)
                        .Build();
        }

        class SampleInterceptor : IConsumeInterceptor
        {
            public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        }

        class SamplePubInterceptor : IPublishInterceptor
        {
            public ValueTask Intercept(PublishContext context, CancellationToken cancellationToken = default) 
                => ValueTask.CompletedTask;
        }
    }

}
