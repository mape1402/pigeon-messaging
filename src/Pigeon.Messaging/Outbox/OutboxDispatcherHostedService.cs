namespace Pigeon.Messaging.Outbox
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Producing.Management;

    internal sealed class OutboxDispatcherHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOutboxDispatchQueue _dispatchQueue;
        private readonly GlobalSettings _settings;
        private readonly ILogger<OutboxDispatcherHostedService> _logger;
        private DateTimeOffset _nextCleanOnUtc = DateTimeOffset.MinValue;

        public OutboxDispatcherHostedService(
            IServiceScopeFactory scopeFactory,
            IOutboxDispatchQueue dispatchQueue,
            IOptions<GlobalSettings> settings,
            ILogger<OutboxDispatcherHostedService> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _dispatchQueue = dispatchQueue ?? throw new ArgumentNullException(nameof(dispatchQueue));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_settings.Outbox?.Enabled != true)
                return;

            await InitializeSchemaAsync(stoppingToken);

            var queueWorker = ProcessQueuedMessagesAsync(stoppingToken);
            var recoveryWorker = RecoverPendingMessagesAsync(stoppingToken);

            await Task.WhenAll(queueWorker, recoveryWorker);
        }

        private async Task ProcessQueuedMessagesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var messageId = await _dispatchQueue.DequeueAsync(cancellationToken);
                    await DispatchMessageAsync(messageId, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox queue dispatcher failed.");
                }
            }
        }

        private async Task RecoverPendingMessagesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await QueuePendingMessagesAsync(cancellationToken);
                    await CleanPublishedAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox recovery dispatcher failed.");
                }

                await Task.Delay(GetDispatchInterval(), cancellationToken);
            }
        }

        private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
        {
            if (_settings.Outbox.SchemaMode != OutboxSchemaMode.AutoCreate)
                return;

            using var scope = _scopeFactory.CreateScope();
            var initializers = scope.ServiceProvider.GetServices<IOutboxSchemaInitializer>();

            foreach (var initializer in initializers)
                await initializer.InitializeAsync(cancellationToken);
        }

        private async Task QueuePendingMessagesAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetService<IOutboxStorage>();

            if (storage == null)
                throw new InvalidOperationException("Pigeon outbox is enabled but no IOutboxStorage has been registered.");

            var now = DateTimeOffset.UtcNow;
            var messages = await storage.LockPendingAsync(GetDispatchBatchSize(), GetLockTimeout(), now, cancellationToken);

            if (messages.Count == 0)
                return;

            foreach (var message in messages)
                await _dispatchQueue.EnqueueAsync(message.Id, cancellationToken);
        }

        private async Task DispatchMessageAsync(Guid messageId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetService<IOutboxStorage>();

            if (storage == null)
                throw new InvalidOperationException("Pigeon outbox is enabled but no IOutboxStorage has been registered.");

            var message = await storage.LockAsync(messageId, GetLockTimeout(), DateTimeOffset.UtcNow, cancellationToken);

            if (message == null)
                return;

            await storage.SaveChangesAsync(cancellationToken);

            var manager = scope.ServiceProvider.GetRequiredService<IProducingManager>();

            try
            {
                await manager.PushOutboxAsync(message, cancellationToken);
                await storage.MarkPublishedAsync(message.Id, DateTimeOffset.UtcNow, cancellationToken);
            }
            catch (Exception ex)
            {
                var nextAttempt = message.Attempts + 1 >= GetMaxRetries()
                    ? (DateTimeOffset?)null
                    : DateTimeOffset.UtcNow.Add(GetRetryDelay());

                await storage.MarkFailedAsync(message.Id, ex.ToString(), DateTimeOffset.UtcNow, nextAttempt, cancellationToken);
                _logger.LogError(ex, "Outbox message {OutboxMessageId} failed to publish.", message.Id);
            }

            await storage.SaveChangesAsync(cancellationToken);
        }

        private async Task CleanPublishedAsync(CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;

            if (now < _nextCleanOnUtc)
                return;

            _nextCleanOnUtc = now.Add(GetCleanInterval());

            using var scope = _scopeFactory.CreateScope();
            var storage = scope.ServiceProvider.GetService<IOutboxStorage>();

            if (storage == null)
                return;

            var olderThan = now.Subtract(GetRetention());
            var deleted = await storage.CleanPublishedAsync(olderThan, GetCleanBatchSize(), cancellationToken);

            if (deleted > 0)
                await storage.SaveChangesAsync(cancellationToken);
        }

        private TimeSpan GetDispatchInterval()
            => _settings.Outbox.DispatchInterval <= TimeSpan.Zero ? TimeSpan.FromMinutes(1) : _settings.Outbox.DispatchInterval;

        private TimeSpan GetCleanInterval()
            => _settings.Outbox.CleanInterval <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : _settings.Outbox.CleanInterval;

        private TimeSpan GetRetention()
            => _settings.Outbox.PublishedMessageRetention <= TimeSpan.Zero ? TimeSpan.FromDays(1) : _settings.Outbox.PublishedMessageRetention;

        private int GetDispatchBatchSize()
            => Math.Max(1, _settings.Outbox.DispatchBatchSize);

        private int GetCleanBatchSize()
            => Math.Max(1, _settings.Outbox.CleanBatchSize);

        private int GetMaxRetries()
            => Math.Max(1, _settings.Outbox.MaxRetries);

        private TimeSpan GetRetryDelay()
            => _settings.Outbox.RetryDelay <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : _settings.Outbox.RetryDelay;

        private TimeSpan GetLockTimeout()
            => _settings.Outbox.LockTimeout <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : _settings.Outbox.LockTimeout;
    }
}
