namespace Pigeon.Messaging.Tests.Consuming.Management
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Topology;
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

        private static IOptions<GlobalSettings> CreateAutomaticAckOptions()
            => Options.Create(new GlobalSettings
            {
                Domain = "test-domain",
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess
                }
            });

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
        public async Task StartAsync_Ensures_Configured_Consume_Topology_Before_Starting_Adapters()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var topologyProvisioningService = Substitute.For<ITopologyProvisioningService>();
            var endpoint = new ConsumerEndpoint("orders.created", "billing");
            consumingConfigurator.GetAllEndpoints().Returns(new[] { endpoint });
            var manager = new ConsumingManager(
                dispatcher,
                new[] { adapter },
                consumingConfigurator,
                topologyProvisioningService,
                CreateOptions(),
                logger);

            await manager.StartAsync();

            Received.InOrder(() =>
            {
                topologyProvisioningService.EnsureConsumeTopologyAsync(endpoint, Arg.Any<CancellationToken>());
                adapter.StartConsumeAsync(Arg.Any<CancellationToken>());
            });
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

            await dispatcher.Received(1).DispatchAsync(
                "topic1",
                "Default",
                Arg.Any<RawPayload>(),
                Arg.Any<Func<CancellationToken, Task>>(),
                Arg.Any<Func<Exception, CancellationToken, Task>>(),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MessageConsumed_Completes_Message_When_Dispatch_Succeeds()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var completed = false;
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, CreateAutomaticAckOptions(), logger);

            await manager.StartAsync();

            adapter.MessageConsumed += Raise.EventWith(
                adapter,
                new MessageConsumedEventArgs(
                    "topic1",
                    RawJson,
                    "Default",
                    _ =>
                    {
                        completed = true;
                        return Task.CompletedTask;
                    },
                    (_, _) => Task.CompletedTask));

            await Task.Delay(100);
            await manager.StopAsync();

            Assert.True(completed);
        }

        [Fact]
        public async Task MessageConsumed_Does_Not_Complete_Message_By_Default()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var completed = false;
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, CreateOptions(), logger);

            await manager.StartAsync();

            adapter.MessageConsumed += Raise.EventWith(
                adapter,
                new MessageConsumedEventArgs(
                    "topic1",
                    RawJson,
                    "Default",
                    _ =>
                    {
                        completed = true;
                        return Task.CompletedTask;
                    },
                    (_, _) => Task.CompletedTask));

            await Task.Delay(100);
            await manager.StopAsync();

            Assert.False(completed);
        }

        [Fact]
        public async Task MessageConsumed_Fails_Message_When_Dispatch_Fails()
        {
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            dispatcher
                .DispatchAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<RawPayload>(),
                    Arg.Any<Func<CancellationToken, Task>>(),
                    Arg.Any<Func<Exception, CancellationToken, Task>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => throw new InvalidOperationException("dispatch failed"));
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var failed = false;
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, CreateAutomaticAckOptions(), logger);

            await manager.StartAsync();

            adapter.MessageConsumed += Raise.EventWith(
                adapter,
                new MessageConsumedEventArgs(
                    "topic1",
                    RawJson,
                    "Default",
                    _ => Task.CompletedTask,
                    (_, _) =>
                    {
                        failed = true;
                        return Task.CompletedTask;
                    }));

            await Task.Delay(100);
            await manager.StopAsync();

            Assert.True(failed);
        }

        [Fact]
        public async Task MessageConsumed_Should_Not_Limit_Concurrency_By_Default()
        {
            var totalMessages = 5;
            var started = 0;
            var active = 0;
            var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            dispatcher
                .DispatchAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<RawPayload>(),
                    Arg.Any<Func<CancellationToken, Task>>(),
                    Arg.Any<Func<Exception, CancellationToken, Task>>(),
                    Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    Interlocked.Increment(ref active);

                    if (Interlocked.Increment(ref started) == totalMessages)
                        allStarted.TrySetResult();

                    await release.Task;
                    Interlocked.Decrement(ref active);
                });
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, CreateOptions(), logger);

            await manager.StartAsync();

            for (var i = 0; i < totalMessages; i++)
                adapter.MessageConsumed += Raise.EventWith(adapter, new MessageConsumedEventArgs("topic1", RawJson));

            await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(totalMessages, Volatile.Read(ref active));

            release.SetResult();
            await manager.StopAsync();
        }

        [Fact]
        public async Task MessageConsumed_Should_Respect_Configured_MaxConcurrency()
        {
            var started = 0;
            var active = 0;
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var dispatcher = Substitute.For<IConsumingDispatcher>();
            dispatcher
                .DispatchAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<RawPayload>(),
                    Arg.Any<Func<CancellationToken, Task>>(),
                    Arg.Any<Func<Exception, CancellationToken, Task>>(),
                    Arg.Any<CancellationToken>())
                .Returns(async _ =>
                {
                    Interlocked.Increment(ref active);
                    Interlocked.Increment(ref started);
                    firstStarted.TrySetResult();
                    await release.Task;
                    Interlocked.Decrement(ref active);
                });
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var options = Options.Create(new GlobalSettings
            {
                Domain = "test-domain",
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    MaxConcurrency = 1
                }
            });
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, options, logger);

            await manager.StartAsync();

            adapter.MessageConsumed += Raise.EventWith(adapter, new MessageConsumedEventArgs("topic1", RawJson));
            adapter.MessageConsumed += Raise.EventWith(adapter, new MessageConsumedEventArgs("topic1", RawJson));

            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(100);

            Assert.Equal(1, Volatile.Read(ref active));
            Assert.Equal(1, Volatile.Read(ref started));

            release.SetResult();
            await WaitUntilAsync(() => Volatile.Read(ref started) == 2);
            await manager.StopAsync();
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var timeout = DateTimeOffset.UtcNow.AddSeconds(2);

            while (DateTimeOffset.UtcNow < timeout)
            {
                if (condition())
                    return;

                await Task.Delay(25);
            }

            throw new TimeoutException("The expected condition was not reached.");
        }
    }
}
