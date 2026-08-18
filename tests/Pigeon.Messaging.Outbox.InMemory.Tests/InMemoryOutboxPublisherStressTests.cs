namespace Pigeon.Messaging.Outbox.InMemory.Tests
{
    using System.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Outbox.InMemory;
    using Pigeon.Messaging.Producing;

    public class InMemoryOutboxPublisherStressTests
    {
        [Fact]
        public async Task Same_Thread_Publish_Should_Store_High_Message_Volume_In_Real_InMemory_Outbox()
        {
            if (!IsStressEnabled())
                return;

            var messageCount = GetMessageCount();
            var services = new ServiceCollection().AddLogging();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Stress"
                })
                .Build();

            services.AddPigeon(configuration, builder =>
            {
                builder.UseInMemoryBroker();
                builder.UseInMemoryOutbox(settings =>
                {
                    settings.ImmediateDispatch = false;
                    settings.DispatchInterval = TimeSpan.FromMinutes(10);
                    settings.CleanInterval = TimeSpan.FromMinutes(10);
                });
            });

            await using var provider = services.BuildServiceProvider();
            var producer = provider.GetRequiredService<IProducer>();
            var outbox = provider.GetRequiredService<IInMemoryOutbox>();

            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < messageCount; i++)
            {
                await producer.PublishAsync(
                    new StressOutboxMessage { Id = i, Text = "hello" },
                    "stress.real-outbox.publisher");
            }

            stopwatch.Stop();

            Assert.Equal(messageCount, outbox.Messages.Count);
            Assert.All(outbox.Messages, message => Assert.Equal(OutboxMessageStatus.Pending, message.Status));

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds <= 0 ? 1 : stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Same-thread real in-memory outbox publish stored {messageCount:N0} messages in {stopwatch.Elapsed}.");
            Console.WriteLine($"Throughput: {messageCount / elapsedSeconds:N0} messages/sec.");
        }

        private static bool IsStressEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("PIGEON_STRESS_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

        private static int GetMessageCount()
        {
            var raw = Environment.GetEnvironmentVariable("PIGEON_STRESS_MESSAGES");
            return int.TryParse(raw, out var value) && value > 0 ? value : 100_000;
        }

        private sealed class StressOutboxMessage
        {
            public int Id { get; set; }

            public string Text { get; set; }
        }
    }
}
