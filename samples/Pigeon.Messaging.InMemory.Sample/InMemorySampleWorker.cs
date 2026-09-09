namespace Pigeon.Messaging.InMemory.Sample
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.InMemory;
    using Pigeon.Messaging.Producing;

    internal sealed class InMemorySampleWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly InMemorySampleScenario _scenario;
        private readonly IInMemoryBroker _broker;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<InMemorySampleWorker> _logger;

        public InMemorySampleWorker(
            IServiceScopeFactory scopeFactory,
            InMemorySampleScenario scenario,
            IInMemoryBroker broker,
            IHostApplicationLifetime lifetime,
            ILogger<InMemorySampleWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _scenario = scenario;
            _broker = broker;
            _lifetime = lifetime;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

            using var scope = _scopeFactory.CreateScope();
            var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
            var consumerInvoker = scope.ServiceProvider.GetRequiredService<IPigeonConsumerInvoker>();

            _logger.LogInformation("Publishing OrderCreatedMessage {OrderId} to the in-memory broker.", _scenario.OrderId);
            await producer.PublishAsync(new OrderCreatedMessage { OrderId = _scenario.OrderId }, OrderRoutes.CreatedTopic, stoppingToken);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);

            try
            {
                await _scenario.WaitForAuditAsync(linked.Token);
                var envelope = await _scenario.WaitForDeferredEnvelopeAsync(linked.Token);

                _logger.LogInformation(
                    "Replaying deferred billing envelope for topic {Topic}, subscription {Subscription}.",
                    envelope.Topic,
                    envelope.Subscription);

                await consumerInvoker.InvokeAsync(envelope, linked.Token);

                await _scenario.WaitForBothModulesAsync(linked.Token);
                await WaitForCompletedDeliveriesAsync(2, linked.Token);

                _logger.LogInformation("Audit consumed inline and billing consumed through deferred replay.");
                _logger.LogInformation("Published messages: {PublishedMessages}", _broker.PublishedMessages.Count);
                _logger.LogInformation("Deliveries: {Deliveries}", _broker.Deliveries.Count);

                foreach (var delivery in _broker.Deliveries)
                {
                    _logger.LogInformation(
                        "Delivery to {Subscription}: completed={Completed}, failed={Failed}",
                        delivery.Subscription,
                        delivery.Completed,
                        delivery.Failed);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("In-memory sample timed out waiting for both consumers.");
                Environment.ExitCode = 1;
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }

        private async Task WaitForCompletedDeliveriesAsync(int expectedDeliveries, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var deliveries = _broker.Deliveries;

                if (deliveries.Count == expectedDeliveries && deliveries.All(delivery => delivery.Completed))
                    return;

                await Task.Delay(50, cancellationToken);
            }
        }
    }
}
