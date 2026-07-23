namespace Pigeon.Messaging.Outbox.InMemory.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Outbox.InMemory;
    using Pigeon.Messaging.Producing;

    public class InMemoryOutboxIntegrationTests
    {
        [Fact]
        public async Task PublishAsync_Should_Store_In_Outbox_And_Dispatch_To_InMemoryBroker()
        {
            var services = new ServiceCollection().AddLogging();
            services.AddPigeon(CreateConfiguration(), builder =>
            {
                builder.UseInMemoryBroker();
                builder.UseInMemoryOutbox(settings =>
                {
                    settings.DispatchInterval = TimeSpan.FromMinutes(10);
                    settings.CleanInterval = TimeSpan.FromMinutes(10);
                });
            });

            await using var provider = services.BuildServiceProvider();
            await StartHostedServicesAsync(provider);

            using var scope = provider.CreateScope();
            var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
            await producer.PublishAsync(new TestMessage { Text = "hello" }, "tests.outbox");

            var broker = provider.GetRequiredService<IInMemoryBroker>();
            await WaitUntilAsync(() => broker.PublishedMessages.Count == 1);

            var outbox = provider.GetRequiredService<IInMemoryOutbox>();
            Assert.Single(outbox.Messages);
            Assert.Equal(OutboxMessageStatus.Published, outbox.Messages.Single().Status);

            await StopHostedServicesAsync(provider);
        }

        private static IConfiguration CreateConfiguration()
            => new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Tests"
                })
                .Build();

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

        private sealed class TestMessage
        {
            public string Text { get; set; }
        }
    }
}
