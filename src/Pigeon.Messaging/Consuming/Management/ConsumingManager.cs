namespace Pigeon.Messaging.Consuming.Management
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Consuming.Dispatching;

    internal class ConsumingManager : IConsumingManager
    {
        private readonly IConsumingDispatcher _dispatcher;
        private readonly IEnumerable<IMessageBrokerAdapter> _messageBrokerAdapters;
        private readonly ILogger<ConsumingManager> _logger;

        private CancellationToken _backgroundCancellationToken;

        public ConsumingManager(IConsumingDispatcher dispatcher, IEnumerable<IMessageBrokerAdapter> messageBrokerAdapters, ILogger<ConsumingManager> logger)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBrokerAdapters = messageBrokerAdapters ?? throw new ArgumentNullException(nameof(messageBrokerAdapters));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _backgroundCancellationToken = cancellationToken;

            foreach (var adapter in _messageBrokerAdapters)
            {
                adapter.MessageConsumed += MessageConsumed;
                await adapter.StartConsumeAsync(cancellationToken);
            } 
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            foreach (var adapter in _messageBrokerAdapters)
            {
                adapter.MessageConsumed -= MessageConsumed;

                try
                {
                    await adapter.StopConsumeAsync(_backgroundCancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to stop adapter {adapter.GetType().Name} gracefully.");
                }
            }
        }

        private void MessageConsumed(object sender, MessageConsumedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    var cts = new CancellationTokenSource(); // TODO: maybe include a timeout global or by consumer =)

                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _backgroundCancellationToken);

                    var rawPayload = new RawPayload(e.RawPayload);

                    await _dispatcher.DispatchAsync(e.Topic, rawPayload, linkedCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Has occurred an unexpected error when a message has been consumed.");
                }
            });
        }
    }
}
