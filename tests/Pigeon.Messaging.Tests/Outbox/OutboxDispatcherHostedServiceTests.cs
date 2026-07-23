namespace Pigeon.Messaging.Tests.Outbox
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;

    public class OutboxDispatcherHostedServiceTests
    {
        [Fact]
        public async Task Dispatcher_Should_Mark_Message_For_Retry_When_Publish_Fails()
        {
            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = "{}",
                PayloadType = typeof(TestMessage).AssemblyQualifiedName,
                Topic = "orders.created",
                RoutingKey = "orders.created",
                Status = OutboxMessageStatus.Pending
            };
            var storage = new TestOutboxStorage(message);
            var services = new ServiceCollection();
            var settings = Options.Create(new GlobalSettings
            {
                Outbox = new OutboxSettings
                {
                    Enabled = true,
                    DispatchInterval = TimeSpan.FromMinutes(5),
                    RetryDelay = TimeSpan.FromSeconds(10),
                    MaxRetries = 2,
                    SchemaMode = OutboxSchemaMode.Migrations
                }
            });
            var dispatchQueue = new ChannelOutboxDispatchQueue(settings);

            services.AddScoped<IOutboxStorage>(_ => storage);
            services.AddSingleton<IProducingManager>(new ThrowingProducingManager());

            var dispatcher = new OutboxDispatcherHostedService(
                services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
                dispatchQueue,
                settings,
                Substitute.For<ILogger<OutboxDispatcherHostedService>>());

            await dispatcher.StartAsync(CancellationToken.None);

            await dispatchQueue.EnqueueAsync(message.Id);
            await storage.MessageFailed.Task.WaitAsync(TimeSpan.FromSeconds(2));

            await dispatcher.StopAsync(CancellationToken.None);

            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(1, message.Attempts);
            Assert.NotNull(message.NextAttemptOnUtc);
            Assert.Contains("publish failed", message.LastError);
            Assert.True(storage.SaveChangesCount >= 2);
        }

        private sealed class TestMessage
        {
            public string Text { get; set; }
        }

        private sealed class TestOutboxStorage : IOutboxStorage
        {
            private readonly OutboxMessage _message;

            public TestOutboxStorage(OutboxMessage message)
            {
                _message = message;
            }

            public TaskCompletionSource MessageFailed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int SaveChangesCount { get; private set; }

            public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task<IReadOnlyCollection<OutboxMessage>> LockPendingAsync(int batchSize, TimeSpan lockTimeout, DateTimeOffset now, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyCollection<OutboxMessage>>(Array.Empty<OutboxMessage>());

            public Task<OutboxMessage> LockAsync(Guid id, TimeSpan lockTimeout, DateTimeOffset now, CancellationToken cancellationToken = default)
            {
                _message.Status = OutboxMessageStatus.Locked;
                _message.LockedOnUtc = now;
                return Task.FromResult(_message);
            }

            public Task MarkPublishedAsync(Guid id, DateTimeOffset publishedOnUtc, CancellationToken cancellationToken = default)
                => Task.CompletedTask;

            public Task MarkFailedAsync(Guid id, string error, DateTimeOffset now, DateTimeOffset? nextAttemptOnUtc, CancellationToken cancellationToken = default)
            {
                _message.Attempts++;
                _message.Status = nextAttemptOnUtc == null ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
                _message.LastError = error;
                _message.LockedOnUtc = null;
                _message.NextAttemptOnUtc = nextAttemptOnUtc;
                MessageFailed.TrySetResult();
                return Task.CompletedTask;
            }

            public Task<int> CleanPublishedAsync(DateTimeOffset olderThanUtc, int batchSize, CancellationToken cancellationToken = default)
                => Task.FromResult(0);

            public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                SaveChangesCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingProducingManager : IProducingManager
        {
            public ValueTask PushAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PushAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PushRawAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PushRawAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PushOutboxAsync(OutboxMessage message, CancellationToken cancellationToken = default)
                => ValueTask.FromException(new InvalidOperationException("publish failed"));
        }
    }
}
