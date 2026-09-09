namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Contains information about a consumed raw message, including its topic and payload.
    /// </summary>
    public class MessageConsumedEventArgs : EventArgs, IMessageSettlement
    {
        /// <summary>
        /// The topic or channel from which the message was consumed.
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// The raw message payload, typically as a JSON string.
        /// </summary>
        public string RawPayload { get; }

        /// <summary>
        /// The subscription, queue name, or consumer group that consumed the message.
        /// </summary>
        public string Subscription { get; }

        private readonly Func<CancellationToken, Task> _completeAsync;
        private readonly Func<Exception, CancellationToken, Task> _failAsync;
        private readonly Func<Exception, CancellationToken, Task> _retryAsync;
        private readonly Func<Exception, CancellationToken, Task> _rejectAsync;
        private int _acknowledged;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsumedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload of the message.</param>
        public MessageConsumedEventArgs(string topic, string rawPayload)
            : this(topic, rawPayload, Configuration.ConsumerEndpoint.DefaultSubscription)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsumedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload of the message.</param>
        /// <param name="subscription">The subscription that consumed the message.</param>
        public MessageConsumedEventArgs(string topic, string rawPayload, string subscription)
            : this(topic, rawPayload, subscription, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsumedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload of the message.</param>
        /// <param name="subscription">The subscription that consumed the message.</param>
        /// <param name="completeAsync">Callback invoked when the message is handled successfully.</param>
        /// <param name="failAsync">Callback invoked when the message handling fails.</param>
        public MessageConsumedEventArgs(
            string topic,
            string rawPayload,
            string subscription,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync)
            : this(topic, rawPayload, subscription, completeAsync, failAsync, failAsync, failAsync)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsumedEventArgs"/> class.
        /// </summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload of the message.</param>
        /// <param name="subscription">The subscription that consumed the message.</param>
        /// <param name="completeAsync">Callback invoked when the message is handled successfully.</param>
        /// <param name="failAsync">Callback invoked when generic message handling fails.</param>
        /// <param name="retryAsync">Callback invoked when the message should be retried.</param>
        /// <param name="rejectAsync">Callback invoked when the message should be rejected.</param>
        public MessageConsumedEventArgs(
            string topic,
            string rawPayload,
            string subscription,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync,
            Func<Exception, CancellationToken, Task> retryAsync,
            Func<Exception, CancellationToken, Task> rejectAsync)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            RawPayload = rawPayload ?? throw new ArgumentNullException(nameof(rawPayload));
            Subscription = string.IsNullOrWhiteSpace(subscription) ? Configuration.ConsumerEndpoint.DefaultSubscription : subscription;
            _completeAsync = completeAsync ?? (_ => Task.CompletedTask);
            _failAsync = failAsync ?? ((_, _) => Task.CompletedTask);
            _retryAsync = retryAsync ?? _failAsync;
            _rejectAsync = rejectAsync ?? _failAsync;
        }

        /// <summary>
        /// Confirms successful message processing in the underlying broker.
        /// </summary>
        public Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _acknowledged, 1) == 1)
                return Task.CompletedTask;

            return _completeAsync(cancellationToken);
        }

        /// <summary>
        /// Reports failed message processing to the underlying broker.
        /// </summary>
        public Task FailAsync(Exception exception, CancellationToken cancellationToken = default)
            => RetryAsync(exception, cancellationToken);

        /// <summary>
        /// Requests retry semantics in the underlying broker.
        /// </summary>
        /// <param name="exception">The optional exception that caused the retry.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous retry operation.</returns>
        public Task RetryAsync(Exception exception = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _acknowledged, 1) == 1)
                return Task.CompletedTask;

            return _retryAsync(exception, cancellationToken);
        }

        /// <summary>
        /// Rejects the message in the underlying broker.
        /// </summary>
        /// <param name="exception">The optional exception that caused the rejection.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous rejection operation.</returns>
        public Task RejectAsync(Exception exception = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _acknowledged, 1) == 1)
                return Task.CompletedTask;

            return _rejectAsync(exception, cancellationToken);
        }
    }
}
