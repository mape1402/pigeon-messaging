namespace Pigeon.Testing
{
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Represents a message captured by the Pigeon testing transport.
    /// </summary>
    public sealed class PigeonTestingMessage
    {
        internal PigeonTestingMessage(
            object message,
            string topic,
            PublishingRoute route,
            IReadOnlyDictionary<string, object> headers,
            string rawJson,
            bool isRaw,
            DateTimeOffset createdOnUtc)
        {
            Id = Guid.NewGuid();
            Message = message;
            MessageType = message?.GetType();
            Topic = topic;
            Route = route;
            Headers = headers ?? new Dictionary<string, object>();
            RawJson = rawJson;
            IsRaw = isRaw;
            CreatedOnUtc = createdOnUtc;
        }

        /// <summary>
        /// Gets the captured message identifier.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Gets the business payload.
        /// </summary>
        public object Message { get; }

        /// <summary>
        /// Gets the business payload type.
        /// </summary>
        public Type MessageType { get; }

        /// <summary>
        /// Gets the topic used for publishing or dispatch.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the subscription used during dispatch.
        /// </summary>
        public string Subscription { get; private set; }

        /// <summary>
        /// Gets the captured publishing route.
        /// </summary>
        public PublishingRoute Route { get; }

        /// <summary>
        /// Gets captured metadata headers.
        /// </summary>
        public IReadOnlyDictionary<string, object> Headers { get; }

        /// <summary>
        /// Gets the correlation id when present in headers.
        /// </summary>
        public string CorrelationId => Headers.TryGetValue("correlation-id", out var value) ? value?.ToString() : null;

        /// <summary>
        /// Gets the serialized payload passed through Pigeon dispatch.
        /// </summary>
        public string RawJson { get; }

        /// <summary>
        /// Gets whether the captured message was published as raw payload.
        /// </summary>
        public bool IsRaw { get; }

        /// <summary>
        /// Gets the publish timestamp.
        /// </summary>
        public DateTimeOffset CreatedOnUtc { get; }

        /// <summary>
        /// Gets the number of dispatch failures recorded for this message.
        /// </summary>
        public int RetryAttempts { get; private set; }

        /// <summary>
        /// Gets whether the message was consumed successfully.
        /// </summary>
        public bool Consumed { get; private set; }

        /// <summary>
        /// Gets whether the message ended in dead letter state.
        /// </summary>
        public bool DeadLettered { get; private set; }

        /// <summary>
        /// Gets the failure exception, when the message failed.
        /// </summary>
        public Exception Exception { get; private set; }

        internal PigeonTestingMessage MarkConsumed(string subscription)
        {
            Consumed = true;
            Subscription = subscription;
            return this;
        }

        internal PigeonTestingMessage MarkFailed(Exception exception, string subscription)
        {
            RetryAttempts++;
            DeadLettered = true;
            Exception = exception;
            Subscription = subscription;
            return this;
        }

        internal PigeonTestingMessage CloneForDispatch()
            => new(Message, Topic, Route, Headers, RawJson, IsRaw, CreatedOnUtc)
            {
                RetryAttempts = RetryAttempts
            };
    }
}
