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
            var dispatched = false;
            dispatcher
                .DispatchAsync(
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<RawPayload>(),
                    Arg.Any<Func<CancellationToken, Task>>(),
                    Arg.Any<Func<Exception, CancellationToken, Task>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    dispatched = true;
                    return Task.CompletedTask;
                });
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var options = CreateOptions();
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, options, logger);

            // Start manager to attach event
            await manager.StartAsync();

            var eventArgs = new MessageConsumedEventArgs("topic1", RawJson);

            // Raise event
            adapter.MessageConsumed += Raise.EventWith(adapter, eventArgs);

            await WaitUntilAsync(() => dispatched);

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
        public async Task MessageConsumedAsync_Should_Dispatch_Directly_When_Execution_Is_Unbounded()
        {
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
                .Returns(_ => release.Task);
            var adapter = new TestConsumingAdapter();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var diagnostics = new ConsumerExecutionDiagnostics();
            var manager = new ConsumingManager(
                dispatcher,
                new[] { adapter },
                Substitute.For<IConsumingConfigurator>(),
                Substitute.For<ITopologyProvisioningService>(),
                CreateOptions(),
                diagnostics,
                logger);

            await manager.StartAsync();

            var raise = adapter.RaiseAsync(new MessageConsumedEventArgs("topic1", RawJson)).AsTask();

            await Task.Delay(100);

            Assert.False(raise.IsCompleted);
            Assert.Equal(1, diagnostics.GetSnapshot().ReceivedMessages);
            Assert.Equal(0, diagnostics.GetSnapshot().QueuedMessages);

            release.SetResult();
            await raise.WaitAsync(TimeSpan.FromSeconds(2));
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

        [Fact]
        public async Task MessageConsumed_Should_Apply_Backpressure_When_Bounded_Queue_Is_Full()
        {
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
                .Returns(_ => release.Task);
            var adapter = new TestConsumingAdapter();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var options = Options.Create(new GlobalSettings
            {
                Domain = "test-domain",
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    MaxConcurrency = 1,
                    QueueCapacity = 1
                }
            });
            var manager = new ConsumingManager(dispatcher, new[] { adapter }, options, logger);

            await manager.StartAsync();

            await adapter.RaiseAsync(new MessageConsumedEventArgs("topic1", RawJson));
            await adapter.RaiseAsync(new MessageConsumedEventArgs("topic1", RawJson));

            var blockedRaise = adapter.RaiseAsync(new MessageConsumedEventArgs("topic1", RawJson)).AsTask();

            await Task.Delay(150);

            Assert.False(blockedRaise.IsCompleted);

            release.SetResult();
            await blockedRaise.WaitAsync(TimeSpan.FromSeconds(2));
            await manager.StopAsync();
        }

        [Fact]
        public async Task Diagnostics_Should_Report_Internal_Backlog()
        {
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
                .Returns(_ => release.Task);
            var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
            var logger = Substitute.For<ILogger<ConsumingManager>>();
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var topologyProvisioningService = Substitute.For<ITopologyProvisioningService>();
            var diagnostics = new ConsumerExecutionDiagnostics();
            var options = Options.Create(new GlobalSettings
            {
                Domain = "test-domain",
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    MaxConcurrency = 1,
                    QueueCapacity = 10,
                    PrefetchCount = 5
                }
            });
            var manager = new ConsumingManager(
                dispatcher,
                new[] { adapter },
                consumingConfigurator,
                topologyProvisioningService,
                options,
                diagnostics,
                logger);

            await manager.StartAsync();

            adapter.MessageConsumed += Raise.EventWith(adapter, new MessageConsumedEventArgs("topic1", RawJson));
            adapter.MessageConsumed += Raise.EventWith(adapter, new MessageConsumedEventArgs("topic1", RawJson));

            await WaitUntilAsync(() => diagnostics.GetSnapshot().ActiveHandlers == 1 && diagnostics.GetSnapshot().QueuedMessages == 1);

            var snapshot = diagnostics.GetSnapshot();

            Assert.Equal(2, snapshot.ReceivedMessages);
            Assert.Equal(1, snapshot.ActiveHandlers);
            Assert.Equal(1, snapshot.QueuedMessages);
            Assert.True(snapshot.IsQueueBounded);
            Assert.Equal(10, snapshot.QueueCapacity);
            Assert.Equal(1, snapshot.MaxConcurrency);
            Assert.Equal((ushort)5, snapshot.PrefetchCount);

            release.SetResult();
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

        private sealed class TestConsumingAdapter : IMessageBrokerConsumingAdapter
        {
            public event EventHandler<MessageConsumedEventArgs> MessageConsumed;

            public event MessageConsumedAsyncHandler MessageConsumedAsync;

            public ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;

            public ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
                => ValueTask.CompletedTask;

            public async ValueTask RaiseAsync(MessageConsumedEventArgs args, CancellationToken cancellationToken = default)
            {
                var asyncHandler = MessageConsumedAsync;
                if (asyncHandler == null)
                {
                    MessageConsumed?.Invoke(this, args);
                    return;
                }

                foreach (MessageConsumedAsyncHandler handler in asyncHandler.GetInvocationList())
                    await handler(this, args, cancellationToken);
            }
        }
    }
}
