namespace Pigeon.Testing
{
    /// <summary>
    /// Provides an in-memory testing surface for publishing messages, dispatching pending messages,
    /// and inspecting broker-like outcomes without requiring a real broker.
    /// </summary>
    public interface IPigeonTestingTransport
    {
        /// <summary>
        /// Gets the business payloads published through the testing transport.
        /// </summary>
        IReadOnlyCollection<object> Messages { get; }

        /// <summary>
        /// Gets the full published message envelopes captured by the testing transport.
        /// </summary>
        IReadOnlyCollection<PigeonTestingMessage> PublishedMessages { get; }

        /// <summary>
        /// Gets the messages successfully consumed after pending dispatch.
        /// </summary>
        IReadOnlyCollection<PigeonTestingMessage> ConsumedMessages { get; }

        /// <summary>
        /// Gets messages that failed infrastructure simulation or consumer execution.
        /// </summary>
        IReadOnlyCollection<PigeonTestingMessage> DeadLetterMessages { get; }

        /// <summary>
        /// Gets recorded consumer and infrastructure failures.
        /// </summary>
        IReadOnlyCollection<PigeonTestingFailure> ConsumerFailures { get; }

        /// <summary>
        /// Publishes a message to a topic inferred from the message type name.
        /// </summary>
        ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Publishes a message to the specified topic.
        /// </summary>
        ValueTask PublishAsync<T>(T message, string topic, CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Dispatches all pending published messages to registered consumers.
        /// </summary>
        Task DispatchPendingAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Simulates a failure the next time a message of type <typeparamref name="T"/> is dispatched.
        /// </summary>
        void FailNext<T>(Exception exception) where T : class;

        /// <summary>
        /// Clears captured messages, pending messages, and configured failures.
        /// </summary>
        void Clear();
    }
}
