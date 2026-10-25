namespace Pigeon.Messaging.InMemory.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Producing;

    public class InMemoryBrokerTests
    {
        [Fact]
        public async Task PublishAsync_Should_Fan_Out_To_All_Matching_Subscriptions()
        {
            var services = CreateServices();
            var billingReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var auditReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            services
                .AddPigeon(CreateConfiguration(), pigeon =>
                {
                    pigeon.ConfigureConsumerExecution(settings =>
                    {
                        settings.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess;
                    });

                    pigeon.UseInMemoryBroker();
                })
                .AddConsumeHandler<OrderCreatedMessage>(
                    "orders.created",
                    SemanticVersion.Default,
                    "billing",
                    (context, message) =>
                    {
                        billingReceived.TrySetResult();
                        return Task.CompletedTask;
                    })
                .AddConsumeHandler<OrderCreatedMessage>(
                    "orders.created",
                    SemanticVersion.Default,
                    "audit",
                    (context, message) =>
                    {
                        auditReceived.TrySetResult();
                        return Task.CompletedTask;
                    });

            await using var provider = services.BuildServiceProvider();
            await StartHostedServicesAsync(provider);

            var producer = provider.CreateScope().ServiceProvider.GetRequiredService<IProducer>();
            await producer.PublishAsync(
                new OrderCreatedMessage { OrderId = "order-1" },
                "orders.exchange",
                "orders.created",
                SemanticVersion.Default);

            await Task.WhenAll(
                billingReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)),
                auditReceived.Task.WaitAsync(TimeSpan.FromSeconds(5)));

            var broker = provider.GetRequiredService<IInMemoryBroker>();
            await WaitUntilAsync(() => broker.Deliveries.Count == 2 && broker.Deliveries.All(delivery => delivery.Completed));

            Assert.Single(broker.PublishedMessages);
            Assert.Equal(2, broker.Deliveries.Count);
            Assert.Contains(broker.Deliveries, delivery => delivery.Subscription == "billing");
            Assert.Contains(broker.Deliveries, delivery => delivery.Subscription == "audit");

            await StopHostedServicesAsync(provider);
        }

        [Fact]
        public async Task PublishRawAsync_Should_Capture_Message_Without_Dispatching_To_Pigeon_Consumers()
        {
            var services = CreateServices();
            var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            services
                .AddPigeon(CreateConfiguration(), pigeon =>
                {
                    pigeon.UseInMemoryBroker();
                })
                .AddConsumeHandler<OrderCreatedMessage>(
                    "orders.created",
                    SemanticVersion.Default,
                    "audit",
                    (context, message) =>
                    {
                        received.TrySetResult();
                        return Task.CompletedTask;
                    });

            await using var provider = services.BuildServiceProvider();
            await StartHostedServicesAsync(provider);

            var producer = provider.CreateScope().ServiceProvider.GetRequiredService<IProducer>();
            await producer.PublishRawAsync(new OrderCreatedMessage { OrderId = "raw-1" }, "orders.created");
            await Task.Delay(250);

            var broker = provider.GetRequiredService<IInMemoryBroker>();

            Assert.False(received.Task.IsCompleted);
            Assert.Single(broker.PublishedMessages);
            Assert.True(broker.PublishedMessages.Single().IsRaw);
            Assert.Empty(broker.Deliveries);

            await StopHostedServicesAsync(provider);
        }

        [Fact]
        public async Task ObserverFailure_RedeliversOnlyAffectedSubscription()
        {
            var services = CreateServices();
            var state = new RetryObserverState();
            services.AddSingleton(state);
            var pigeon = services.AddPigeon(CreateConfiguration(), settings =>
            {
                settings.ConfigureConsumerExecution(execution => execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess);
                settings.UseInMemoryBroker(options => options.RetryDelay = TimeSpan.FromMilliseconds(10));
            });
            pigeon.AddConsumeDecisionInterceptor<RetryTestDecisionInterceptor>();
            pigeon.AddConsumeDecisionObserver<RetryTestObserver>();
            pigeon.AddConsumeHandler<OrderCreatedMessage>("orders.created", SemanticVersion.Default, "billing", (_, _) => throw new InvalidOperationException("Handler must not run."));
            pigeon.AddConsumeHandler<OrderCreatedMessage>("orders.created", SemanticVersion.Default, "audit", (_, _) => throw new InvalidOperationException("Handler must not run."));
            await using var provider = services.BuildServiceProvider();
            await StartHostedServicesAsync(provider);

            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IProducer>().PublishAsync(new OrderCreatedMessage { OrderId = "order-1" },
                "orders.exchange", "orders.created", SemanticVersion.Default);
            var broker = provider.GetRequiredService<IInMemoryBroker>();
            await WaitUntilAsync(() => broker.Deliveries.Count == 3 && broker.Deliveries.Count(d => d.Completed) == 2);

            var billing = broker.Deliveries.Where(d => d.Subscription == "billing").OrderBy(d => d.Attempt).ToArray();
            Assert.Equal(new[] { 1, 2 }, billing.Select(d => d.Attempt));
            Assert.True(billing[0].Failed);
            Assert.True(billing[1].Completed);
            Assert.Equal(billing[0].MessageId, billing[1].MessageId);
            Assert.Single(broker.Deliveries.Where(d => d.Subscription == "audit"));
            await StopHostedServicesAsync(provider);
        }

        [Fact]
        public async Task OptionalObserverFailure_CompletesWithoutRedelivery()
        {
            var services = CreateServices();
            services.AddSingleton(new RetryObserverState());
            var pigeon = services.AddPigeon(CreateConfiguration(), settings =>
            {
                settings.ConfigureConsumerExecution(execution => execution.AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess);
                settings.UseInMemoryBroker(options => options.RetryDelay = TimeSpan.FromMilliseconds(10));
            });
            pigeon.AddConsumeDecisionInterceptor<RetryTestDecisionInterceptor>();
            pigeon.AddConsumeDecisionObserver<RetryTestObserver>(ObserverFailureBehavior.Continue);
            pigeon.AddConsumeHandler<OrderCreatedMessage>("orders.created", SemanticVersion.Default, "billing", (_, _) => throw new InvalidOperationException());
            await using var provider = services.BuildServiceProvider();
            await StartHostedServicesAsync(provider);

            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IProducer>().PublishAsync(new OrderCreatedMessage { OrderId = "order-1" },
                "orders.exchange", "orders.created", SemanticVersion.Default);
            var broker = provider.GetRequiredService<IInMemoryBroker>();
            await WaitUntilAsync(() => broker.Deliveries.Count == 1 && broker.Deliveries.Single().Completed);
            await Task.Delay(50);

            Assert.Single(broker.Deliveries);
            Assert.True(broker.Deliveries.Single().Completed);
            await StopHostedServicesAsync(provider);
        }

        private static IServiceCollection CreateServices()
            => new ServiceCollection().AddLogging();

        private static IConfiguration CreateConfiguration()
            => new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Pigeon:Domain"] = "Tests"
                })
                .Build();

        private static async Task StartHostedServicesAsync(IServiceProvider provider)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>())
                await hostedService.StartAsync(CancellationToken.None);
        }

        private static async Task StopHostedServicesAsync(IServiceProvider provider)
        {
            foreach (var hostedService in provider.GetServices<IHostedService>().Reverse())
                await hostedService.StopAsync(CancellationToken.None);
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            var timeout = DateTimeOffset.UtcNow.AddSeconds(5);

            while (DateTimeOffset.UtcNow < timeout)
            {
                if (condition())
                    return;

                await Task.Delay(50);
            }

            throw new TimeoutException("The expected in-memory broker state was not reached.");
        }

        private sealed class OrderCreatedMessage
        {
            public string OrderId { get; set; }
        }
    }
}
