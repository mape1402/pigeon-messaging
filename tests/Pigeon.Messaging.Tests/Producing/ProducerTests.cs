namespace Pigeon.Messaging.Tests.Producing
{
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using System.Text.Json;

    public class ProducerTests
    {
        [Fact]
        public void Constructor_Should_Throw_If_Deps_Null()
        {
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test" };
            var interceptors = Enumerable.Empty<IPublishInterceptor>();
            var options = Options.Create(settings);

            Assert.Throws<ArgumentNullException>(() => new Producer(null, manager, options));
            Assert.Throws<ArgumentNullException>(() => new Producer(interceptors, null, options));
            Assert.Throws<ArgumentNullException>(() => new Producer(interceptors, manager, null));
        }

        [Fact]
        public async Task PublishAsync_Should_Call_PublishCore()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test-domain" };
            var options = Options.Create(settings);

            var producer = new Producer(new[] { interceptor }, manager, options);

            await producer.PublishAsync("msg", "topic");

            await interceptor.Received(1).Intercept(Arg.Any<PublishContext>());
            await manager.Received(1).PushAsync(
                Arg.Any<WrappedPayload<string>>(),
                Arg.Is<PublishingRoute>(route =>
                    route.Topic == "topic" &&
                    route.RoutingKey == "topic" &&
                    route.Exchange == string.Empty),
                CancellationToken.None);
        }

        [Fact]
        public async Task PublishAsync_Should_Call_Manager_With_Exchange_Route()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test-domain" };
            var options = Options.Create(settings);
            var producer = new Producer(new[] { interceptor }, manager, options);

            await producer.PublishAsync("msg", "events", "user.created", "1.0.0");

            await interceptor.Received(1).Intercept(Arg.Any<PublishContext>());
            await manager.Received(1).PushAsync(
                Arg.Any<WrappedPayload<string>>(),
                Arg.Is<PublishingRoute>(route =>
                    route.Topic == "user.created" &&
                    route.RoutingKey == "user.created" &&
                    route.Exchange == "events"),
                CancellationToken.None);
        }

        [Fact]
        public async Task PublishRawAsync_Should_Call_Manager_Without_Interceptors()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test-domain" };
            var options = Options.Create(settings);
            var producer = new Producer(new[] { interceptor }, manager, options);

            await producer.PublishRawAsync("msg", "topic");

            await interceptor.DidNotReceive().Intercept(Arg.Any<PublishContext>());
            await manager.Received(1).PushRawAsync(
                "msg",
                Arg.Is<PublishingRoute>(route =>
                    route.Topic == "topic" &&
                    route.RoutingKey == "topic" &&
                    route.Exchange == string.Empty),
                CancellationToken.None);
            await manager.DidNotReceive().PushAsync(Arg.Any<WrappedPayload<string>>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishRawAsync_Should_Call_Manager_With_Exchange_Route()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test-domain" };
            var options = Options.Create(settings);
            var producer = new Producer(new[] { interceptor }, manager, options);

            await producer.PublishRawAsync("msg", "events", "user.created");

            await interceptor.DidNotReceive().Intercept(Arg.Any<PublishContext>());
            await manager.Received(1).PushRawAsync(
                "msg",
                Arg.Is<PublishingRoute>(route =>
                    route.Topic == "user.created" &&
                    route.RoutingKey == "user.created" &&
                    route.Exchange == "events"),
                CancellationToken.None);
        }

        [Fact]
        public async Task PublishAsync_Should_Store_Final_Intercepted_Payload_When_Outbox_Enabled()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            interceptor
                .Intercept(Arg.Any<PublishContext>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    call.Arg<PublishContext>().AddMetadata("correlationId", "abc");
                    return ValueTask.CompletedTask;
                });

            var manager = Substitute.For<IProducingManager>();
            var storage = new TestOutboxStorage();
            var serializer = new TestSerializer();
            var settings = new GlobalSettings
            {
                Domain = "test-domain",
                Outbox = new OutboxSettings { Enabled = true }
            };
            var producer = new Producer(
                new[] { interceptor },
                manager,
                Options.Create(settings),
                storage,
                new OutboxMessageFactory(serializer));

            await producer.PublishAsync(new TestMessage { Text = "hello" }, "events", "orders.created", "1.0.0");

            await manager.DidNotReceive().PushAsync(
                Arg.Any<WrappedPayload<TestMessage>>(),
                Arg.Any<PublishingRoute>(),
                Arg.Any<CancellationToken>());

            var stored = Assert.Single(storage.Messages);
            Assert.Equal(1, storage.SaveChangesCount);
            Assert.False(stored.IsRaw);
            Assert.Equal("events", stored.Exchange);
            Assert.Equal("orders.created", stored.RoutingKey);

            var payload = (WrappedPayload<TestMessage>)serializer.Deserialize(
                stored.Payload,
                Type.GetType(stored.PayloadType, throwOnError: true));

            Assert.Equal("test-domain", payload.Domain);
            Assert.Equal("hello", payload.Message.Text);
            Assert.Equal("abc", payload.Metadata["correlationId"].ToString());
        }

        [Fact]
        public async Task PublishRawAsync_Should_Store_Raw_Payload_When_Outbox_Enabled()
        {
            var manager = Substitute.For<IProducingManager>();
            var storage = new TestOutboxStorage();
            var serializer = new TestSerializer();
            var settings = new GlobalSettings
            {
                Domain = "test-domain",
                Outbox = new OutboxSettings { Enabled = true }
            };
            var producer = new Producer(
                Enumerable.Empty<IPublishInterceptor>(),
                manager,
                Options.Create(settings),
                storage,
                new OutboxMessageFactory(serializer));

            await producer.PublishRawAsync(new TestMessage { Text = "raw" }, "topic");

            await manager.DidNotReceive().PushRawAsync(
                Arg.Any<TestMessage>(),
                Arg.Any<PublishingRoute>(),
                Arg.Any<CancellationToken>());

            var stored = Assert.Single(storage.Messages);
            Assert.Equal(1, storage.SaveChangesCount);
            Assert.True(stored.IsRaw);
            Assert.Equal("topic", stored.Topic);

            var payload = (TestMessage)serializer.Deserialize(stored.Payload, Type.GetType(stored.PayloadType, throwOnError: true));
            Assert.Equal("raw", payload.Text);
        }

        [Fact]
        public async Task PublishAsync_Should_Throw_If_Message_Null()
        {
            var producer = GetProducer();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await producer.PublishAsync<string>(null, "topic"));
        }

        [Fact]
        public async Task PublishRawAsync_Should_Throw_If_Message_Null()
        {
            var producer = GetProducer();
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await producer.PublishRawAsync<string>(null, "topic"));
        }

        [Fact]
        public async Task PublishAsync_Should_Throw_If_Topic_Empty()
        {
            var producer = GetProducer();
            await Assert.ThrowsAsync<ArgumentException>(async () => await producer.PublishAsync("msg", ""));
        }

        [Fact]
        public async Task PublishRawAsync_Should_Throw_If_Topic_Empty()
        {
            var producer = GetProducer();
            await Assert.ThrowsAsync<ArgumentException>(async () => await producer.PublishRawAsync("msg", ""));
        }

        private Producer GetProducer()
        {
            var interceptor = Substitute.For<IPublishInterceptor>();
            var manager = Substitute.For<IProducingManager>();
            var settings = new GlobalSettings { Domain = "test" };
            var options = Options.Create(settings);
            return new Producer(new[] { interceptor }, manager, options);
        }

        private sealed class TestMessage
        {
            public string Text { get; set; }
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

        private sealed class TestOutboxStorage : IOutboxStorage
        {
            public List<OutboxMessage> Messages { get; } = new();

            public int SaveChangesCount { get; private set; }

            public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
            {
                Messages.Add(message);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyCollection<OutboxMessage>> LockPendingAsync(int batchSize, TimeSpan lockTimeout, DateTimeOffset now, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyCollection<OutboxMessage>>(Array.Empty<OutboxMessage>());

            public Task<OutboxMessage> LockAsync(Guid id, TimeSpan lockTimeout, DateTimeOffset now, CancellationToken cancellationToken = default)
                => Task.FromResult<OutboxMessage>(null);

            public Task MarkPublishedAsync(Guid id, DateTimeOffset publishedOnUtc, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task MarkFailedAsync(Guid id, string error, DateTimeOffset now, DateTimeOffset? nextAttemptOnUtc, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<int> CleanPublishedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken = default)
                => Task.FromResult(0);

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCount++;
                return Task.CompletedTask;
            }
        }
    }
}
