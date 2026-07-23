namespace Pigeon.Messaging.Rabbit.Sample
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.EntityFrameworkCore;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Contracts;
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
}
