namespace Pigeon.Messaging.Tests.Consuming.Management
{
    using System.Diagnostics;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Topology;

    public class ConsumingManagerStressTests
    {
        private const string RawJson = @"{""Domain"":""stress"",""MessageVersion"":""1.0.0"",""CreatedOnUtc"":""2026-08-18T00:00:00Z"",""Message"":{""Text"":""Hello""},""Metadata"":{}}";

        [Fact]
        public async Task Direct_Unbounded_Consume_Dispatch_Should_Handle_High_Message_Volume()
        {
            if (!IsStressEnabled())
                return;

            var messageCount = GetMessageCount();
            var adapter = new StressConsumingAdapter();
            var dispatcher = new CountingDispatcher();
            var diagnostics = new ConsumerExecutionDiagnostics();
            var manager = new ConsumingManager(
                dispatcher,
                new[] { adapter },
                new ConsumingConfigurator(),
                NoopTopologyProvisioningService.Instance,
                Options.Create(new GlobalSettings
                {
                    Domain = "stress",
                    ConsumerExecution = new ConsumerExecutionSettings
                    {
                        AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess
                    }
                }),
                diagnostics,
                NullLogger<ConsumingManager>.Instance);

            await manager.StartAsync();

            var stopwatch = Stopwatch.StartNew();
            var completed = 0;
            var messages = Enumerable.Range(0, messageCount);

            await Parallel.ForEachAsync(
                messages,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 8 },
                async (_, cancellationToken) =>
                {
                    await adapter.RaiseAsync(
                        new MessageConsumedEventArgs(
                            "stress.topic",
                            RawJson,
                            "Default",
                            _ =>
                            {
                                Interlocked.Increment(ref completed);
                                return Task.CompletedTask;
                            },
                            (_, _) => Task.CompletedTask),
                        cancellationToken);
                });

            stopwatch.Stop();
            await manager.StopAsync();

            var snapshot = diagnostics.GetSnapshot();

            Assert.Equal(messageCount, dispatcher.DispatchedMessages);
            Assert.Equal(messageCount, completed);
            Assert.Equal(messageCount, snapshot.ReceivedMessages);
            Assert.Equal(0, snapshot.QueuedMessages);
            Assert.Equal(messageCount, snapshot.CompletedHandlers);
            Assert.Equal(messageCount, snapshot.AcknowledgedMessages);
            Assert.Equal(0, snapshot.FailedHandlers);

            Console.WriteLine($"Direct unbounded consume dispatch processed {messageCount:N0} messages in {stopwatch.Elapsed}.");
            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds <= 0 ? 1 : stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Throughput: {messageCount / elapsedSeconds:N0} messages/sec.");
        }

        private static bool IsStressEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("PIGEON_STRESS_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

        private static int GetMessageCount()
        {
            var raw = Environment.GetEnvironmentVariable("PIGEON_STRESS_MESSAGES");
            return int.TryParse(raw, out var value) && value > 0 ? value : 100_000;
        }

        private sealed class CountingDispatcher : IConsumingDispatcher
        {
            private int _dispatchedMessages;

            public int DispatchedMessages => Volatile.Read(ref _dispatchedMessages);

            public Task DispatchAsync(string topic, RawPayload rawPayload, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _dispatchedMessages);
                return Task.CompletedTask;
            }

            public Task DispatchAsync(string topic, string subscription, RawPayload rawPayload, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _dispatchedMessages);
                return Task.CompletedTask;
            }

            public Task DispatchAsync(
                string topic,
                string subscription,
                RawPayload rawPayload,
                Func<CancellationToken, Task> completeAsync,
                Func<Exception, CancellationToken, Task> failAsync,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _dispatchedMessages);
                return Task.CompletedTask;
            }
        }

        private sealed class StressConsumingAdapter : IMessageBrokerConsumingAdapter
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
