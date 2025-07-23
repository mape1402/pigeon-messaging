namespace Pigeon.Messaging.Tests.Consuming.Management
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ConsumingManagerTests
    {
        private const string RawJson = @"{
            ""Domain"": ""test-domain"",
            ""MessageVersion"": ""1.2.3"",
            ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
            ""Message"": { ""Text"": ""Hello"" },
            ""Metadata"": { ""Key"": { ""Prop"": ""Value"" } }
        }";

        private static IOptions<GlobalSettings> CreateOptions() => Options.Create(new GlobalSettings { Domain = "test-domain" });

        [Fact]
        public async Task StartAsync_RegistersEventsAndStartsAdapters()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            var adapter1 = Substitute.For<IMessageBrokerConsumingAdapter>();
            var adapter2 = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var options = CreateOptions();
            var manager = new ConsumingManager(dispatcher, new[] { adapter1, adapter2 }, options, logger);

            await manager.StartAsync();

            await adapter1.Received(1).StartConsumeAsync(Arg.Any<CancellationToken>());
            await adapter2.Received(1).StartConsumeAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task StopAsync_UnregistersEventsAndStopsAdapters()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var options = CreateOptions();
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, options, logger);

            // Start first to attach event
            await manager.StartAsync();
            await manager.StopAsync();

            await adapter.Received(1).StopConsumeAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MessageConsumed_InvokesDispatchAsync()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var options = CreateOptions();
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, options, logger);

            // Start manager to attach event
            await manager.StartAsync();

            var eventArgs = new MessageConsumedEventArgs("topic1", RawJson);

            // Raise event
            adapter.MessageConsumed += Raise.EventWith(adapter, eventArgs);

            // Allow some delay for async fire-and-forget
            await Task.Delay(100);

            await dispatcher.Received(1).DispatchAsync("topic1", Arg.Any<RawPayload>(), Arg.Any<CancellationToken>());
        }
    }
}
