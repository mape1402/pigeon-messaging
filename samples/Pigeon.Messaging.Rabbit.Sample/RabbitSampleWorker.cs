namespace Pigeon.Messaging.Rabbit.Sample
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;

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
}
