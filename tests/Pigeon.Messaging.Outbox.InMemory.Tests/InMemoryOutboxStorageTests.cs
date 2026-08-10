namespace Pigeon.Messaging.Outbox.InMemory.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Mule;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Outbox.InMemory;
    using Pigeon.Messaging.Producing;
    using System.Transactions;

    public class InMemoryOutboxStorageTests
    {
        [Fact]
        public async Task PublishAsync_Should_Store_Message_As_Pending_Durable_Action_When_Immediate_Dispatch_Is_Disabled()
        {
            await using var provider = CreateServiceProvider(settings =>
            {
                settings.ImmediateDispatch = false;
                settings.DispatchInterval = TimeSpan.FromMinutes(10);
            });
            var producer = provider.GetRequiredService<IProducer>();

            await producer.PublishAsync(new InMemoryOutboxTestMessage { Text = "hello" }, "tests.outbox");

            var outbox = provider.GetRequiredService<IInMemoryOutbox>();
            var stored = Assert.Single(outbox.Messages);

            Assert.Equal(OutboxMessageStatus.Pending, stored.Status);
            Assert.Equal("tests.outbox", stored.Topic);
            Assert.Empty(provider.GetRequiredService<IInMemoryBroker>().PublishedMessages);
        }

        [Fact]
        public async Task PublishAsync_Should_Dispatch_To_InMemoryBroker_When_Hosted_Service_Runs()
        {
            await using var provider = CreateServiceProvider(settings =>
            {
                settings.DispatchInterval = TimeSpan.FromMinutes(10);
                settings.CleanInterval = TimeSpan.FromMinutes(10);
            });
            await StartHostedServicesAsync(provider);

            var producer = provider.GetRequiredService<IProducer>();

            await producer.PublishAsync(new InMemoryOutboxTestMessage { Text = "hello" }, "tests.outbox");

            var broker = provider.GetRequiredService<IInMemoryBroker>();
            await WaitUntilAsync(() => broker.PublishedMessages.Count == 1);

            var outbox = provider.GetRequiredService<IInMemoryOutbox>();
            Assert.Equal(OutboxMessageStatus.Published, outbox.Messages.Single().Status);

            await StopHostedServicesAsync(provider);
        }

        [Fact]
        public async Task Diagnostics_Should_Return_Message_Counts_From_Mule()
        {
            await using var provider = CreateServiceProvider(settings =>
            {
                settings.ImmediateDispatch = false;
                settings.DispatchInterval = TimeSpan.FromMinutes(10);
            });
            var mule = provider.GetRequiredService<IMuleClient>();
            var diagnostics = provider.GetRequiredService<IOutboxDiagnostics>();

            await mule.EnqueueAsync(PigeonOutboxActionKeys.Publish, CreateMessage());

            var snapshot = await diagnostics.GetSnapshotAsync();

            Assert.Equal(1, snapshot.PendingMessages);
            Assert.Equal(0, snapshot.FailedMessages);
            Assert.NotNull(snapshot.OldestPendingMessageOnUtc);
        }

        [Fact]
        public async Task PublishAsync_Should_Persist_After_Ambient_Transaction_Commits()
        {
            await using var provider = CreateServiceProvider(settings =>
            {
                settings.ImmediateDispatch = false;
                settings.DispatchInterval = TimeSpan.FromMinutes(10);
            });
            var producer = provider.GetRequiredService<IProducer>();
            var outbox = provider.GetRequiredService<IInMemoryOutbox>();

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await producer.PublishAsync(new InMemoryOutboxTestMessage { Text = "hello" }, "tests.outbox");

                Assert.Empty(outbox.Messages);

                transaction.Complete();
            }

            Assert.Single(outbox.Messages);
        }

        [Fact]
        public async Task PublishAsync_Should_Discard_When_Ambient_Transaction_Rolls_Back()
        {
            await using var provider = CreateServiceProvider(settings =>
            {
                settings.ImmediateDispatch = false;
                settings.DispatchInterval = TimeSpan.FromMinutes(10);
            });
            var producer = provider.GetRequiredService<IProducer>();

            using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await producer.PublishAsync(new InMemoryOutboxTestMessage { Text = "hello" }, "tests.outbox");
            }

            Assert.Empty(provider.GetRequiredService<IInMemoryOutbox>().Messages);
        }

        private static ServiceProvider CreateServiceProvider(Action<OutboxSettings> configureOutbox = null)
        {
            var services = new ServiceCollection().AddLogging();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Tests"
                })
                .Build();

            services.AddPigeon(configuration, builder =>
            {
                builder.UseInMemoryBroker();
                builder.UseInMemoryOutbox(configureOutbox);
            });

            return services.BuildServiceProvider();
        }

        private static OutboxMessage CreateMessage()
            => new()
            {
                Id = Guid.NewGuid(),
                Payload = "{}",
                PayloadType = typeof(object).AssemblyQualifiedName,
                Topic = "topic",
                RoutingKey = "topic",
                CreatedOnUtc = DateTimeOffset.UtcNow
            };

        private static async Task StartHostedServicesAsync(IServiceProvider provider)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>())
                await hostedService.StartAsync(CancellationToken.None);
        }

        private static async Task StopHostedServicesAsync(IServiceProvider provider)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>().Reverse())
                await hostedService.StopAsync(CancellationToken.None);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var timeout = DateTimeOffset.UtcNow.AddSeconds(5);

            while (DateTimeOffset.UtcNow < timeout)
            {
                if (condition())
                    return;

                await Task.Delay(50);
            }

            throw new TimeoutException("The expected in-memory broker state was not reached.");
        }
    }
}
