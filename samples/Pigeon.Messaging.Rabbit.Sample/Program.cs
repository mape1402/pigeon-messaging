namespace Pigeon.Messaging.Rabbit.Sample
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Contracts;
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
            var routingKey = $"{routingKeyPrefix}.{runId}";
            var billingQueue = $"{queuePrefix}.billing.{runId}";
            var auditQueue = $"{queuePrefix}.audit.{runId}";

            builder.Services.AddSingleton(new RabbitSampleScenario(
                runId,
                routingKey,
                billingQueue,
                auditQueue,
                builder.Configuration.GetValue<int?>("Sample:TimeoutSeconds") ?? 30,
                acknowledgementMode));

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
                    scenario.MarkBilling(message.OrderId, context.Subscription);
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
                    scenario.MarkAudit(message.OrderId, context.Subscription);
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
        private readonly IProducer _producer;
        private readonly RabbitSampleScenario _scenario;
        private readonly IConfiguration _configuration;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<RabbitSampleWorker> _logger;

        public RabbitSampleWorker(
            IProducer producer,
            RabbitSampleScenario scenario,
            IConfiguration configuration,
            IHostApplicationLifetime lifetime,
            ILogger<RabbitSampleWorker> logger)
        {
            _producer = producer;
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
            await _producer.PublishAsync(message, exchange, _scenario.RoutingKey, SemanticVersion.Default, stoppingToken);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_scenario.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);

            try
            {
                await _scenario.WaitForBothConsumersAsync(linked.Token);
                _logger.LogInformation("Rabbit e2e sample completed. Both queues received the message.");
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
            MessageAcknowledgementMode acknowledgementMode)
        {
            RunId = runId;
            RoutingKey = routingKey;
            BillingQueue = billingQueue;
            AuditQueue = auditQueue;
            TimeoutSeconds = timeoutSeconds;
            AcknowledgementMode = acknowledgementMode;
        }

        public string RunId { get; }

        public string RoutingKey { get; }

        public string BillingQueue { get; }

        public string AuditQueue { get; }

        public int TimeoutSeconds { get; }

        public MessageAcknowledgementMode AcknowledgementMode { get; }

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
}
