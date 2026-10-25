using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using RabbitMQ.Client;

namespace Pigeon.Messaging.RabbitMq.Tests.Consuming;

public class RabbitObserverIntegrationTests
{
    [Fact]
    public async Task CriticalObserverFailure_RequeuesBeforeApplyingTerminalDecision()
    {
        var uri = Environment.GetEnvironmentVariable("PIGEON_RABBIT_TEST_URI");
        if (string.IsNullOrWhiteSpace(uri))
            return;

        var queue = "pigeon-observer-" + Guid.NewGuid().ToString("N");
        await using var connection = await new ConnectionFactory { Uri = new Uri(uri) }.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: false, autoDelete: false);
        try
        {
            var state = new RabbitObserverState();
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton(state);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Pigeon:Domain"] = "" }).Build();
            var pigeon = services.AddPigeon(configuration, settings =>
            {
                settings.ConfigureConsumerExecution(execution => execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess);
                settings.UseRabbitMq(rabbit => { rabbit.Url = uri; rabbit.RequeueOnFailure = false; });
            });
            pigeon.AddConsumeDecisionInterceptor<RabbitObserverDecisionInterceptor>();
            pigeon.AddConsumeDecisionObserver<RabbitFailOnceObserver>();
            pigeon.AddConsumeHandler<RabbitObserverMessage>(queue, SemanticVersion.Default, (_, _) => throw new InvalidOperationException("Handler must not run."));

            await using var provider = services.BuildServiceProvider();
            var hosted = provider.GetServices<IHostedService>().ToArray();
            foreach (var service in hosted)
                await service.StartAsync(CancellationToken.None);
            try
            {
                using var scope = provider.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IProducer>().PublishAsync(new RabbitObserverMessage { Value = "test" }, queue);
                await state.Recovered.Task.WaitAsync(TimeSpan.FromSeconds(20));
                Assert.Equal(2, state.Attempts);
                await Task.Delay(200);
            }
            finally
            {
                foreach (var service in hosted.AsEnumerable().Reverse())
                    await service.StopAsync(CancellationToken.None);
            }
            Assert.Null(await channel.BasicGetAsync(queue, autoAck: false));
        }
        finally
        {
            await channel.QueueDeleteAsync(queue);
        }
    }
}
