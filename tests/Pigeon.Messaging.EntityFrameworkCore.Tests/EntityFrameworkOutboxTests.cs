namespace Pigeon.Messaging.EntityFrameworkCore.Tests
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

        private static ServiceProvider BuildProvider(SqliteConnection connection)
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "test-domain"
                })
                .Build();

            services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));

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
