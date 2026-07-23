namespace Pigeon.Messaging.Outbox.InMemory.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Outbox.InMemory;
    using System.Transactions;

    public class InMemoryOutboxStorageTests
    {
        [Fact]
        public async Task SaveChangesAsync_Should_Persist_Staged_Message()
        {
            var storage = CreateStorage(out var outbox);
            var message = CreateMessage();

            await storage.AddAsync(message);
            Assert.Empty(outbox.Messages);

            await storage.SaveChangesAsync();

            var stored = Assert.Single(outbox.Messages);
            Assert.Equal(message.Id, stored.Id);
            Assert.Equal(OutboxMessageStatus.Pending, stored.Status);
        }

        [Fact]
        public async Task LockAsync_Should_Lock_Pending_Message()
        {
            var storage = CreateStorage(out _);
            var message = CreateMessage();
            var now = DateTimeOffset.UtcNow;

            await storage.AddAsync(message);
            await storage.SaveChangesAsync();

            var locked = await storage.LockAsync(message.Id, TimeSpan.FromMinutes(5), now);

            Assert.NotNull(locked);
            Assert.Equal(OutboxMessageStatus.Locked, locked.Status);
            Assert.Equal(now, locked.LockedOnUtc);
        }

        [Fact]
        public async Task MarkFailedAsync_Should_Schedule_Retry_When_NextAttempt_Is_Provided()
        {
            var storage = CreateStorage(out var outbox);
            var message = CreateMessage();
            var nextAttempt = DateTimeOffset.UtcNow.AddMinutes(1);

            await storage.AddAsync(message);
            await storage.SaveChangesAsync();
            await storage.MarkFailedAsync(message.Id, "boom", DateTimeOffset.UtcNow, nextAttempt);

            var stored = outbox.Messages.Single();
            Assert.Equal(OutboxMessageStatus.Pending, stored.Status);
            Assert.Equal(1, stored.Attempts);
            Assert.Equal("boom", stored.LastError);
            Assert.Equal(nextAttempt, stored.NextAttemptOnUtc);
        }

        [Fact]
        public async Task CleanPublishedAsync_Should_Delete_Published_Messages_Older_Than_Cutoff()
        {
            var storage = CreateStorage(out var outbox);
            var oldPublished = CreateMessage();
            var recentPublished = CreateMessage();

            await storage.AddAsync(oldPublished);
            await storage.AddAsync(recentPublished);
            await storage.SaveChangesAsync();
            await storage.MarkPublishedAsync(oldPublished.Id, DateTimeOffset.UtcNow.AddHours(-2));
            await storage.MarkPublishedAsync(recentPublished.Id, DateTimeOffset.UtcNow);

            var deleted = await storage.CleanPublishedAsync(DateTimeOffset.UtcNow.AddHours(-1), 10);

            Assert.Equal(1, deleted);
            Assert.DoesNotContain(outbox.Messages, message => message.Id == oldPublished.Id);
            Assert.Contains(outbox.Messages, message => message.Id == recentPublished.Id);
        }

        [Fact]
        public async Task Diagnostics_Should_Return_Message_Counts()
        {
            var provider = CreateServiceProvider();
            var storage = provider.CreateScope().ServiceProvider.GetRequiredService<IOutboxStorage>();
            var diagnostics = provider.GetRequiredService<IOutboxDiagnostics>();
            var pending = CreateMessage();
            var failed = CreateMessage();

            await storage.AddAsync(pending);
            await storage.AddAsync(failed);
            await storage.SaveChangesAsync();
            await storage.MarkFailedAsync(failed.Id, "failed", DateTimeOffset.UtcNow, null);

            var snapshot = await diagnostics.GetSnapshotAsync();

            Assert.Equal(1, snapshot.PendingMessages);
            Assert.Equal(1, snapshot.FailedMessages);
            Assert.Equal("failed", snapshot.LastFailure);
        }

        [Fact]
        public async Task SaveChangesAsync_Should_Persist_After_Ambient_Transaction_Commits()
        {
            var storage = CreateStorage(out var outbox);
            var message = CreateMessage();

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await storage.AddAsync(message);
                await storage.SaveChangesAsync();

                Assert.Empty(outbox.Messages);

                transaction.Complete();
            }

            Assert.Single(outbox.Messages);
        }

        [Fact]
        public async Task SaveChangesAsync_Should_Discard_When_Ambient_Transaction_Rolls_Back()
        {
            var storage = CreateStorage(out var outbox);

            using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await storage.AddAsync(CreateMessage());
                await storage.SaveChangesAsync();
            }

            Assert.Empty(outbox.Messages);
        }

        private static IOutboxStorage CreateStorage(out IInMemoryOutbox outbox)
        {
            var provider = CreateServiceProvider();
            outbox = provider.GetRequiredService<IInMemoryOutbox>();
            return provider.CreateScope().ServiceProvider.GetRequiredService<IOutboxStorage>();
        }

        private static ServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Tests"
                })
                .Build();

            services.AddPigeon(configuration, builder =>
            {
                builder.UseInMemoryOutbox();
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
    }
}
