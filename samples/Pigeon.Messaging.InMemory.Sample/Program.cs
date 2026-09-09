namespace Pigeon.Messaging.InMemory.Sample
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.InMemory;

    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Pigeon:Domain"] = "in-memory-sample"
            });

            builder.Services.AddSingleton<InMemorySampleScenario>();

            var pigeon = builder.Services
                .AddPigeon(builder.Configuration, settings =>
                {
                    settings.ConfigureConsumerExecution(consumerExecution =>
                    {
                        consumerExecution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
                    });

                    settings.UseInMemoryBroker();
                });

            pigeon
                .AddPublishDecisionInterceptor<SamplePublishDecisionInterceptor>()
                .ForConsumer(OrderRoutes.CreatedForBilling)
                .AddConsumeDecisionInterceptor<DeferredBillingConsumeInterceptor>();

            pigeon
                .AddConsumeHandler<OrderCreatedMessage>(
                    OrderRoutes.CreatedForBilling,
                    (context, message) =>
                    {
                        context.Services.GetRequiredService<InMemorySampleScenario>().MarkBilling(message.OrderId);
                        return Task.CompletedTask;
                    });

            pigeon
                .AddConsumeHandler<OrderCreatedMessage>(
                    OrderRoutes.CreatedForAudit,
                    (context, message) =>
                    {
                        context.Services.GetRequiredService<InMemorySampleScenario>().MarkAudit(message.OrderId);
                        return Task.CompletedTask;
                    });

            builder.Services.AddHostedService<InMemorySampleWorker>();

            await builder.Build().RunAsync();
        }
    }
}
