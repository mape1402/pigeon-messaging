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

        internal bool HasRetryCallback { get; }
        internal bool RequiresSynchronousDispatch { get; }

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
            : this(topic, rawPayload, subscription, completeAsync, failAsync, retryAsync, rejectAsync,
                retryAsync != null || failAsync != null)
        {
        }

        /// <summary>Initializes a delivery and declares whether retry provides redelivery.</summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload.</param>
        /// <param name="subscription">The subscription that consumed the message.</param>
        /// <param name="completeAsync">Callback for successful processing.</param>
        /// <param name="failAsync">Callback for a generic failure.</param>
        /// <param name="retryAsync">Callback for a retry request.</param>
        /// <param name="rejectAsync">Callback for rejection.</param>
        /// <param name="supportsRetry">Whether the retry callback requests redelivery from the transport.</param>
        public MessageConsumedEventArgs(
            string topic,
            string rawPayload,
            string subscription,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync,
            Func<Exception, CancellationToken, Task> retryAsync,
            Func<Exception, CancellationToken, Task> rejectAsync,
            bool supportsRetry)
            : this(topic, rawPayload, subscription, completeAsync, failAsync, retryAsync, rejectAsync, supportsRetry, false)
        {
        }

        /// <summary>Initializes a delivery with explicit retry and dispatch capabilities.</summary>
        /// <param name="topic">The topic the message was consumed from.</param>
        /// <param name="rawPayload">The raw JSON payload.</param>
        /// <param name="subscription">The subscription that consumed the message.</param>
        /// <param name="completeAsync">Callback for successful processing.</param>
        /// <param name="failAsync">Callback for a generic failure.</param>
        /// <param name="retryAsync">Callback for a retry request.</param>
        /// <param name="rejectAsync">Callback for rejection.</param>
        /// <param name="supportsRetry">Whether the retry callback requests redelivery.</param>
        /// <param name="requiresSynchronousDispatch">Whether dispatch must be awaited by the transport listener.</param>
        public MessageConsumedEventArgs(
            string topic,
            string rawPayload,
            string subscription,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync,
            Func<Exception, CancellationToken, Task> retryAsync,
            Func<Exception, CancellationToken, Task> rejectAsync,
            bool supportsRetry,
            bool requiresSynchronousDispatch)
        {
            Topic = topic ?? throw new ArgumentNullException(nameof(topic));
            RawPayload = rawPayload ?? throw new ArgumentNullException(nameof(rawPayload));
            Subscription = string.IsNullOrWhiteSpace(subscription) ? Configuration.ConsumerEndpoint.DefaultSubscription : subscription;
            _completeAsync = completeAsync ?? (_ => Task.CompletedTask);
            _failAsync = failAsync ?? ((_, _) => Task.CompletedTask);
            _retryAsync = retryAsync ?? _failAsync;
            _rejectAsync = rejectAsync ?? _failAsync;
            HasRetryCallback = supportsRetry && (retryAsync != null || failAsync != null);
            RequiresSynchronousDispatch = requiresSynchronousDispatch;
        }

        /// <summary>
        /// Confirms successful message processing in the underlying broker.
        /// </summary>
        public Task CompleteAsync(CancellationToken cancellationToken = default)
            => SettleAsync(() => _completeAsync(cancellationToken));

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
            => SettleAsync(() => _retryAsync(exception, cancellationToken));

        /// <summary>
        /// Rejects the message in the underlying broker.
        /// </summary>
        /// <param name="exception">The optional exception that caused the rejection.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous rejection operation.</returns>
        public Task RejectAsync(Exception exception = null, CancellationToken cancellationToken = default)
            => SettleAsync(() => _rejectAsync(exception, cancellationToken));

        private async Task SettleAsync(Func<Task> settle)
        {
            if (Interlocked.CompareExchange(ref _acknowledged, 1, 0) != 0)
                return;

            try
            {
                await settle().ConfigureAwait(false);
            }
            catch
            {
                Volatile.Write(ref _acknowledged, 0);
                throw;
            }
        }
    }
}
