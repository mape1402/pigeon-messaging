namespace Pigeon.Messaging.Outbox
{
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Creates persisted outbox messages from final producer payloads.
    /// </summary>
    public sealed class OutboxMessageFactory
    {
        private readonly ISerializer _serializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutboxMessageFactory"/> class.
        /// </summary>
        /// <param name="serializer">The serializer used to store payloads.</param>
        public OutboxMessageFactory(ISerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <summary>
        /// Creates an outbox message for the specified payload and publishing route.
        /// </summary>
        /// <param name="payload">The final payload that should later be published.</param>
        /// <param name="route">The route used when dispatching the message.</param>
        /// <param name="isRaw">Whether the payload should be dispatched without a Pigeon wrapper.</param>
        /// <returns>A new outbox message ready to be persisted.</returns>
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
