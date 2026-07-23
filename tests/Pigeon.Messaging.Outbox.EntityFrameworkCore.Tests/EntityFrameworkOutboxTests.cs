namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Tests
{
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using System.Text.Json;

    public class EntityFrameworkOutboxTests
    {
        [Fact]
        public async Task UseEntityFrameworkOutbox_Should_Add_Outbox_Entity_To_App_DbContext_Model()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var provider = BuildProvider(connection);

            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            Assert.NotNull(dbContext.Model.FindEntityType(typeof(OutboxMessage)));
        }

        [Fact]
        public async Task PublishAsync_Should_Persist_Intercepted_Message_In_App_DbContext_Outbox()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var provider = BuildProvider(connection);

            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var producer = scope.ServiceProvider.GetRequiredService<IProducer>();

            await producer.PublishAsync(new TestMessage { Text = "hello" }, "orders.created");

            var stored = await dbContext.Set<OutboxMessage>().SingleAsync();
            var dispatchQueue = scope.ServiceProvider.GetRequiredService<IOutboxDispatchQueue>();
            var queuedMessageId = await dispatchQueue.DequeueAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
            var payloadType = Type.GetType(stored.PayloadType, throwOnError: true);
            var payload = (WrappedPayload<TestMessage>)JsonSerializer.Deserialize(
                stored.Payload,
                payloadType,
                new JsonSerializerOptions { Converters = { new SemanticVersionJsonConverter() } });

            Assert.Equal(stored.Id, queuedMessageId);
            Assert.Equal(OutboxMessageStatus.Pending, stored.Status);
            Assert.Equal("orders.created", stored.Topic);
            Assert.Equal("test-domain", payload.Domain);
            Assert.Equal("hello", payload.Message.Text);
            Assert.Equal("abc", payload.Metadata["correlationId"].ToString());
        }

        [Fact]
        public async Task PublishAsync_Should_Not_Save_Pending_App_DbContext_Changes()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var provider = BuildProvider(connection);

            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            dbContext.BusinessRecords.Add(new BusinessRecord { Name = "not-yet-saved" });

            var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
            await producer.PublishAsync(new TestMessage { Text = "hello" }, "orders.created");

            Assert.Equal(EntityState.Added, dbContext.Entry(dbContext.BusinessRecords.Local.Single()).State);

            using var verificationScope = provider.CreateScope();
            var verificationContext = verificationScope.ServiceProvider.GetRequiredService<TestDbContext>();
            Assert.Equal(0, await verificationContext.BusinessRecords.CountAsync());
            Assert.Equal(1, await verificationContext.Set<OutboxMessage>().CountAsync());
        }

        [Fact]
        public async Task PublishAsync_Should_Persist_Concurrent_Outbox_Messages()
        {
            var databasePath = Path.Combine(Path.GetTempPath(), $"pigeon-outbox-{Guid.NewGuid():N}.db");

            try
            {
                using var provider = BuildProvider($"Data Source={databasePath}");

                using (var scope = provider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                    await dbContext.Database.EnsureCreatedAsync();
                }

                await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
                {
                    using var scope = provider.CreateScope();
                    var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
                    await producer.PublishAsync(new TestMessage { Text = $"hello-{index}" }, "orders.created");
                }));

                using var verificationScope = provider.CreateScope();
                var verificationContext = verificationScope.ServiceProvider.GetRequiredService<TestDbContext>();

                Assert.Equal(20, await verificationContext.Set<OutboxMessage>().CountAsync());
            }
            finally
            {
                DeleteSqliteDatabase(databasePath);
            }
        }

        [Fact]
        public async Task OutboxDiagnostics_Should_Return_Message_Snapshot()
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var provider = BuildProvider(connection);

            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            var now = DateTimeOffset.UtcNow;

            dbContext.Set<OutboxMessage>().AddRange(
                CreateOutboxMessage(OutboxMessageStatus.Pending, now.AddMinutes(-5)),
                CreateOutboxMessage(OutboxMessageStatus.Locked, now.AddMinutes(-4)),
                CreateOutboxMessage(OutboxMessageStatus.Published, now.AddMinutes(-3)),
                CreateOutboxMessage(OutboxMessageStatus.Failed, now.AddMinutes(-2), "last failure"));
            await dbContext.SaveChangesAsync();

            var diagnostics = scope.ServiceProvider.GetRequiredService<IOutboxDiagnostics>();
            var snapshot = await diagnostics.GetSnapshotAsync();

            Assert.Equal(1, snapshot.PendingMessages);
            Assert.Equal(1, snapshot.LockedMessages);
            Assert.Equal(1, snapshot.PublishedMessages);
            Assert.Equal(1, snapshot.FailedMessages);
            Assert.NotNull(snapshot.OldestPendingMessageOnUtc);
            Assert.NotNull(snapshot.OldestFailedMessageOnUtc);
            Assert.Equal("last failure", snapshot.LastFailure);
        }

        private static ServiceProvider BuildProvider(SqliteConnection connection)
            => BuildProvider(options => options.UseSqlite(connection));

        private static ServiceProvider BuildProvider(string connectionString)
            => BuildProvider(options => options.UseSqlite(connectionString));

        private static ServiceProvider BuildProvider(Action<DbContextOptionsBuilder> configureDbContext)
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "test-domain"
                })
                .Build();

            services.AddDbContext<TestDbContext>(configureDbContext);

            services
                .AddPigeon(configuration, pigeon =>
                {
                    pigeon.UseEntityFrameworkOutbox<TestDbContext>();
                    pigeon.AddFeature(feature =>
                    {
                        feature.Services.AddSingleton<IMessageBrokerProducingAdapter, NoopProducingAdapter>();
                    });
                })
                .AddPublishInterceptor<TestPublishInterceptor>();

            return services.BuildServiceProvider();
        }

        private static void DeleteSqliteDatabase(string databasePath)
        {
            SqliteConnection.ClearAllPools();

            if (!File.Exists(databasePath))
                return;

            try
            {
                File.Delete(databasePath);
            }
            catch (IOException)
            {
            }
        }

        private static OutboxMessage CreateOutboxMessage(
            OutboxMessageStatus status,
            DateTimeOffset createdOnUtc,
            string lastError = null)
            => new()
            {
                Id = Guid.NewGuid(),
                Payload = "{}",
                PayloadType = typeof(TestMessage).AssemblyQualifiedName,
                Topic = "orders.created",
                RoutingKey = "orders.created",
                Status = status,
                CreatedOnUtc = createdOnUtc,
                LastError = lastError
            };

        private sealed class TestDbContext : DbContext
        {
            public TestDbContext(DbContextOptions<TestDbContext> options)
                : base(options)
            {
            }

            public DbSet<BusinessRecord> BusinessRecords { get; set; }
        }

        private sealed class BusinessRecord
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class TestPublishInterceptor : IPublishInterceptor
        {
            public ValueTask Intercept(PublishContext context, CancellationToken cancellationToken = default)
            {
                context.AddMetadata("correlationId", "abc");
                return ValueTask.CompletedTask;
            }
        }

        private sealed class TestMessage
        {
            public string Text { get; set; }
        }

        private sealed class NoopProducingAdapter : IMessageBrokerProducingAdapter
        {
            public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;

            public ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
                where T : class
                => ValueTask.CompletedTask;
        }
    }
}
