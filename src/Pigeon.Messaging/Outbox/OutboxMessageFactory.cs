namespace Pigeon.Messaging.Outbox
{
    using Pigeon.Messaging.Producing;

    public sealed class OutboxMessageFactory
    {
        private readonly ISerializer _serializer;

        public OutboxMessageFactory(ISerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public OutboxMessage Create(object payload, PublishingRoute route, bool isRaw)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Payload = _serializer.Serialize(payload),
                PayloadType = payload.GetType().AssemblyQualifiedName,
                IsRaw = isRaw,
                Topic = route.Topic,
                Exchange = route.Exchange,
                RoutingKey = route.RoutingKey,
                Status = OutboxMessageStatus.Pending,
                CreatedOnUtc = DateTimeOffset.UtcNow
            };
        }
    }
}
