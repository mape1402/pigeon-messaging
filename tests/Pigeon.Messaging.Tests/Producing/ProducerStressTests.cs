namespace Pigeon.Messaging.Tests.Producing
{
    using System.Diagnostics;
    using System.Text.Json;
    using Microsoft.Extensions.Options;
    using Mule;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;

    public class ProducerStressTests
    {
        [Fact]
        public async Task Direct_Wrapped_Publish_Should_Handle_High_Message_Volume()
        {
            if (!IsStressEnabled())
                return;

            var messageCount = GetMessageCount();
            var adapter = new CountingProducingAdapter();
            var manager = new ProducingManager(adapter);
            var producer = new Producer(
                Array.Empty<IPublishInterceptor>(),
                manager,
                Options.Create(new GlobalSettings { Domain = "stress" }));

            var stopwatch = Stopwatch.StartNew();

            await Parallel.ForEachAsync(
                Enumerable.Range(0, messageCount),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 8 },
                async (index, cancellationToken) =>
                {
                    await producer.PublishAsync(
                        new StressPublishMessage { Id = index, Text = "hello" },
                        "stress.publisher",
                        cancellationToken);
                });

            stopwatch.Stop();

            Assert.Equal(messageCount, adapter.PublishedMessages);
            Assert.Equal("stress.publisher", adapter.LastRoute?.Topic);

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds <= 0 ? 1 : stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Direct wrapped publish processed {messageCount:N0} messages in {stopwatch.Elapsed}.");
            Console.WriteLine($"Throughput: {messageCount / elapsedSeconds:N0} messages/sec.");
        }

        [Fact]
        public async Task Same_Thread_Outbox_Publish_Should_Handle_High_Message_Volume()
        {
            if (!IsStressEnabled())
                return;

            var messageCount = GetMessageCount();
            var mule = new CountingMuleClient();
            var manager = new ProducingManager(new CountingProducingAdapter());
            var producer = new Producer(
                Array.Empty<IPublishInterceptor>(),
                manager,
                Options.Create(new GlobalSettings
                {
                    Domain = "stress",
                    Outbox = new OutboxSettings
                    {
                        Enabled = true,
                        ImmediateDispatch = false
                    }
                }),
                new OutboxMessageFactory(new TestSerializer()),
                mule);

            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < messageCount; i++)
            {
                await producer.PublishAsync(
                    new StressPublishMessage { Id = i, Text = "hello" },
                    "stress.outbox.publisher");
            }

            stopwatch.Stop();

            Assert.Equal(messageCount, mule.EnqueuedMessages);
            Assert.Equal(PigeonOutboxActionKeys.Publish, mule.LastActionKey);
            Assert.Equal("stress.outbox.publisher", mule.LastMessage?.Topic);

            var elapsedSeconds = stopwatch.Elapsed.TotalSeconds <= 0 ? 1 : stopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Same-thread outbox publish enqueued {messageCount:N0} messages in {stopwatch.Elapsed}.");
            Console.WriteLine($"Throughput: {messageCount / elapsedSeconds:N0} messages/sec.");
        }

        private static bool IsStressEnabled()
            => string.Equals(Environment.GetEnvironmentVariable("PIGEON_STRESS_TESTS"), "true", StringComparison.OrdinalIgnoreCase);

        private static int GetMessageCount()
        {
            var raw = Environment.GetEnvironmentVariable("PIGEON_STRESS_MESSAGES");
            return int.TryParse(raw, out var value) && value > 0 ? value : 100_000;
        }

        private sealed class CountingProducingAdapter : IMessageBrokerProducingAdapter
        {
            private int _publishedMessages;

            public int PublishedMessages => Volatile.Read(ref _publishedMessages);

            public PublishingRoute LastRoute { get; private set; }

            public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
                where T : class
                => PublishMessageAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

            public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
                where T : class
            {
                LastRoute = route;
                Interlocked.Increment(ref _publishedMessages);
                return ValueTask.CompletedTask;
            }

            public ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
                where T : class
                => PublishRawMessageAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

            public ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
                where T : class
            {
                LastRoute = route;
                Interlocked.Increment(ref _publishedMessages);
                return ValueTask.CompletedTask;
            }
        }

        private sealed class CountingMuleClient : IMuleClient
        {
            private int _enqueuedMessages;

            public int EnqueuedMessages => Volatile.Read(ref _enqueuedMessages);

            public ActionKey LastActionKey { get; private set; }

            public OutboxMessage LastMessage { get; private set; }

            public ValueTask<Guid> EnqueueAsync<TPayload>(ActionKey key, TPayload payload, CancellationToken cancellationToken = default)
                => EnqueueAsync(key, payload, null, cancellationToken);

            public ValueTask<Guid> EnqueueAsync<TPayload>(
                ActionKey key,
                TPayload payload,
                Action<EnqueueOptions> configure,
                CancellationToken cancellationToken = default)
            {
                LastActionKey = key;
                LastMessage = (OutboxMessage)(object)payload;
                Interlocked.Increment(ref _enqueuedMessages);
                return ValueTask.FromResult(Guid.NewGuid());
            }

            public ValueTask<IReadOnlyCollection<Guid>> EnqueueManyAsync(
                IEnumerable<MuleIntent> intents,
                CancellationToken cancellationToken = default)
            {
                var ids = new List<Guid>();

                foreach (var intent in intents)
                {
                    LastActionKey = intent.Key;
                    LastMessage = (OutboxMessage)intent.Payload;
                    Interlocked.Increment(ref _enqueuedMessages);
                    ids.Add(Guid.NewGuid());
                }

                return ValueTask.FromResult<IReadOnlyCollection<Guid>>(ids);
            }
        }

        private sealed class TestSerializer : ISerializer
        {
            private readonly JsonSerializerOptions _options = new()
            {
                Converters = { new SemanticVersionJsonConverter() }
            };

            public string Serialize(object payload)
                => JsonSerializer.Serialize(payload, _options);

            public object Deserialize(string rawJson, Type targetType)
                => JsonSerializer.Deserialize(rawJson, targetType, _options);
        }

        private sealed class StressPublishMessage
        {
            public int Id { get; set; }

            public string Text { get; set; }
        }
    }
}
