namespace Pigeon.Messaging.Rabbit.Sample
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.EntityFrameworkCore;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Rabbit;
    using Pigeon.Messaging.Topology;

    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false, reloadOnChange: false);
            builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
            builder.Configuration.AddCommandLine(args);

            var routingKeyPrefix = builder.Configuration["Sample:RoutingKeyPrefix"] ?? "pigeon.sample.orders.created";
            var queuePrefix = builder.Configuration["Sample:QueuePrefix"] ?? "pigeon.sample";
            var acknowledgementMode = builder.Configuration.GetValue<MessageAcknowledgementMode?>("Sample:AcknowledgementMode")
                ?? MessageAcknowledgementMode.OnHandlerSuccess;
            var useOutbox = builder.Configuration.GetValue<bool>("Sample:UseOutbox");
            var outboxDatabasePath = Path.Combine(Path.GetTempPath(), $"pigeon-rabbit-sample-outbox-{runId}.db");
            var routingKey = $"{routingKeyPrefix}.{runId}";
            var billingQueue = $"{queuePrefix}.billing.{runId}";
            var auditQueue = $"{queuePrefix}.audit.{runId}";

            if (useOutbox)
            {
                builder.Services.AddDbContext<RabbitSampleDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={outboxDatabasePath}");
                });
            }

            builder.Services.AddSingleton(new RabbitSampleScenario(
                runId,
                routingKey,
                billingQueue,
                auditQueue,
                builder.Configuration.GetValue<int?>("Sample:TimeoutSeconds") ?? 30,
                acknowledgementMode,
                useOutbox,
                outboxDatabasePath));
            builder.Services.AddScoped<RabbitSampleConsumeContextProbe>();

            var pigeon = builder.Services.AddPigeon(
                builder.Configuration,
                settings =>
                {
                    settings.SetTopologyProvisioningMode(
                        TopologyProvisioningMode.OnStartup |
                        TopologyProvisioningMode.OnPublish |
                        TopologyProvisioningMode.OnConsume);
                    settings.ConfigureConsumerExecution(execution =>
                    {
                        execution.AcknowledgementMode = acknowledgementMode;
                    });

                    if (useOutbox)
                    {
                        settings.UseEntityFrameworkOutbox<RabbitSampleDbContext>(outbox =>
                        {
                            outbox.DispatchInterval = TimeSpan.FromSeconds(1);
                            outbox.CleanInterval = TimeSpan.FromSeconds(5);
                            outbox.PublishedMessageRetention = TimeSpan.FromSeconds(1);
                            outbox.DispatchBatchSize = 10;
                            outbox.CleanBatchSize = 10;
                        });
                    }

                    var rabbitSettings = builder.Configuration
                        .GetSection("RabbitMq")
                        .Get<RabbitSettings>() ?? new RabbitSettings();

                    if (string.IsNullOrWhiteSpace(rabbitSettings.Url))
                        throw new InvalidOperationException("Missing RabbitMq:Url. Set it with dotnet user-secrets set \"RabbitMq:Url\" \"<connection-string>\".");

                    settings.UseRabbitMq(rabbit =>
                    {
                        rabbit.Url = rabbitSettings.Url;
                        rabbit.Exchange = rabbitSettings.Exchange;
                        rabbit.ExchangeType = rabbitSettings.ExchangeType;
                        rabbit.DurableExchange = rabbitSettings.DurableExchange;
                    });
                });

            pigeon.AddConsumeHandler<OrderCreatedMessage>(
                routingKey,
                SemanticVersion.Default,
                billingQueue,
                async (context, message) =>
                {
                    var scenario = context.Services.GetRequiredService<RabbitSampleScenario>();
                    var consumeContextProbe = context.Services.GetRequiredService<RabbitSampleConsumeContextProbe>();
                    scenario.MarkBilling(message.OrderId, consumeContextProbe.GetCurrentSubscription());

                    if (scenario.AcknowledgementMode == MessageAcknowledgementMode.Manual)
                        await context.CompleteAsync();

                    await Task.CompletedTask;
                });

            pigeon.AddConsumeHandler<OrderCreatedMessage>(
                routingKey,
                SemanticVersion.Default,
                auditQueue,
                async (context, message) =>
                {
                    var scenario = context.Services.GetRequiredService<RabbitSampleScenario>();
                    var consumeContextProbe = context.Services.GetRequiredService<RabbitSampleConsumeContextProbe>();
                    scenario.MarkAudit(message.OrderId, consumeContextProbe.GetCurrentSubscription());

                    if (scenario.AcknowledgementMode == MessageAcknowledgementMode.Manual)
                        await context.CompleteAsync();

                    await Task.CompletedTask;
                });

            builder.Services.AddHostedService<RabbitSampleWorker>();

            await builder.Build().RunAsync();
        }
    }

    internal sealed class RabbitSampleWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RabbitSampleScenario _scenario;
        private readonly IConfiguration _configuration;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<RabbitSampleWorker> _logger;

        public RabbitSampleWorker(
            IServiceScopeFactory scopeFactory,
            RabbitSampleScenario scenario,
            IConfiguration configuration,
            IHostApplicationLifetime lifetime,
            ILogger<RabbitSampleWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _scenario = scenario;
            _configuration = configuration;
            _lifetime = lifetime;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            var exchange = _configuration["RabbitMq:Exchange"];
            var message = new OrderCreatedMessage
            {
                OrderId = _scenario.RunId,
                CreatedOnUtc = DateTimeOffset.UtcNow
            };

            _logger.LogInformation("Publishing message to exchange '{Exchange}' with routing key '{RoutingKey}'.", exchange, _scenario.RoutingKey);
            _logger.LogInformation("Acknowledgement mode: {AcknowledgementMode}", _scenario.AcknowledgementMode);
            _logger.LogInformation("Outbox enabled: {UseOutbox}", _scenario.UseOutbox);

            using (var scope = _scopeFactory.CreateScope())
            {
                var consumeContextAccessor = scope.ServiceProvider.GetRequiredService<IConsumeContextAccessor>();
                _logger.LogInformation("Consume context available before publishing: {HasConsumeContext}", consumeContextAccessor.ConsumeContext != null);

                if (_scenario.UseOutbox)
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<RabbitSampleDbContext>();
                    await dbContext.Database.EnsureCreatedAsync(stoppingToken);
                }

                var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
                await producer.PublishAsync(message, exchange, _scenario.RoutingKey, SemanticVersion.Default, stoppingToken);

                if (_scenario.UseOutbox)
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<RabbitSampleDbContext>();
                    var pendingMessages = await dbContext.Set<OutboxMessage>().CountAsync(stoppingToken);
                    _logger.LogInformation("Outbox message persisted. Pending outbox rows: {PendingMessages}", pendingMessages);
                }
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_scenario.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);

            try
            {
                await _scenario.WaitForBothConsumersAsync(linked.Token);
                _logger.LogInformation("Rabbit e2e sample completed. Both queues received the message.");
                if (_scenario.UseOutbox)
                    _logger.LogInformation("Outbox e2e completed. The stored message was dispatched to Rabbit.");

                _logger.LogInformation("Exchange: {Exchange}", exchange);
                _logger.LogInformation("Routing key: {RoutingKey}", _scenario.RoutingKey);
                _logger.LogInformation("Queues: {BillingQueue}, {AuditQueue}", _scenario.BillingQueue, _scenario.AuditQueue);
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Rabbit e2e sample timed out waiting for both consumers.");
                Environment.ExitCode = 1;
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }
    }

    internal sealed class RabbitSampleScenario
    {
        private readonly TaskCompletionSource _billingReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _auditReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RabbitSampleScenario(
            string runId,
            string routingKey,
            string billingQueue,
            string auditQueue,
            int timeoutSeconds,
            MessageAcknowledgementMode acknowledgementMode,
            bool useOutbox,
            string outboxDatabasePath)
        {
            RunId = runId;
            RoutingKey = routingKey;
            BillingQueue = billingQueue;
            AuditQueue = auditQueue;
            TimeoutSeconds = timeoutSeconds;
            AcknowledgementMode = acknowledgementMode;
            UseOutbox = useOutbox;
            OutboxDatabasePath = outboxDatabasePath;
        }

        public string RunId { get; }

        public string RoutingKey { get; }

        public string BillingQueue { get; }

        public string AuditQueue { get; }

        public int TimeoutSeconds { get; }

        public MessageAcknowledgementMode AcknowledgementMode { get; }

        public bool UseOutbox { get; }

        public string OutboxDatabasePath { get; }

        public void MarkBilling(string orderId, string subscription)
        {
            if (orderId == RunId && subscription == BillingQueue)
                _billingReceived.TrySetResult();
        }

        public void MarkAudit(string orderId, string subscription)
        {
            if (orderId == RunId && subscription == AuditQueue)
                _auditReceived.TrySetResult();
        }

        public Task WaitForBothConsumersAsync(CancellationToken cancellationToken)
            => Task.WhenAll(
                _billingReceived.Task.WaitAsync(cancellationToken),
                _auditReceived.Task.WaitAsync(cancellationToken));
    }

    internal sealed class OrderCreatedMessage
    {
        public string OrderId { get; set; }

        public DateTimeOffset CreatedOnUtc { get; set; }
    }

    internal sealed class RabbitSampleConsumeContextProbe
    {
        private readonly IConsumeContextAccessor _consumeContextAccessor;

        public RabbitSampleConsumeContextProbe(IConsumeContextAccessor consumeContextAccessor)
        {
            _consumeContextAccessor = consumeContextAccessor;
        }

        public string GetCurrentSubscription()
        {
            var context = _consumeContextAccessor.ConsumeContext
                ?? throw new InvalidOperationException("No consume context is available outside the Pigeon consume pipeline.");

            return context.Subscription;
        }
    }

    internal sealed class RabbitSampleDbContext : DbContext
    {
        public RabbitSampleDbContext(DbContextOptions<RabbitSampleDbContext> options)
            : base(options)
        {
        }
    }
}
