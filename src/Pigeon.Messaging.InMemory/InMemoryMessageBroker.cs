namespace Pigeon.Messaging.InMemory
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using System.Collections.Concurrent;

    internal sealed class InMemoryMessageBroker : IInMemoryBroker
    {
        private readonly ConcurrentQueue<InMemoryPublishedMessage> _publishedMessages = new();
        private readonly ConcurrentQueue<InMemoryDelivery> _deliveries = new();

        public event Func<InMemoryPublishedMessage, CancellationToken, ValueTask> MessagePublished;

        public IReadOnlyCollection<InMemoryPublishedMessage> PublishedMessages => _publishedMessages.ToArray();

        public IReadOnlyCollection<InMemoryDelivery> Deliveries => _deliveries.ToArray();

        public async ValueTask PublishAsync(string payload, PublishingRoute route, bool isRaw, CancellationToken cancellationToken = default)
        {
            var message = new InMemoryPublishedMessage
            {
                Id = Guid.NewGuid(),
                Payload = payload,
                Route = route,
                IsRaw = isRaw,
                CreatedOnUtc = DateTimeOffset.UtcNow
            };

            _publishedMessages.Enqueue(message);

            var handlers = MessagePublished?
                .GetInvocationList()
                .Cast<Func<InMemoryPublishedMessage, CancellationToken, ValueTask>>()
                .ToArray();

            if (handlers == null)
                return;

            foreach (var handler in handlers)
                await handler(message, cancellationToken);
        }

        public InMemoryDelivery CreateDelivery(InMemoryPublishedMessage message, ConsumerEndpoint endpoint)
        {
            var delivery = new InMemoryDelivery
            {
                MessageId = message.Id,
                Topic = endpoint.Topic,
                Subscription = endpoint.Subscription
            };

            _deliveries.Enqueue(delivery);
            return delivery;
        }

        public void Clear()
        {
            while (_publishedMessages.TryDequeue(out _))
            {
            }

            while (_deliveries.TryDequeue(out _))
            {
            }
        }
    }
}
