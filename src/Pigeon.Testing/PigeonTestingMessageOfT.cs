namespace Pigeon.Testing
{
    /// <summary>
    /// Strongly typed view over a captured testing message.
    /// </summary>
    public sealed class PigeonTestingMessage<T> where T : class
    {
        internal PigeonTestingMessage(PigeonTestingMessage message)
        {
            Source = message ?? throw new ArgumentNullException(nameof(message));
        }

        /// <summary>
        /// Gets the original captured testing message.
        /// </summary>
        public PigeonTestingMessage Source { get; }

        /// <summary>
        /// Gets the typed business payload.
        /// </summary>
        public T Message => (T)Source.Message;

        /// <summary>
        /// Gets captured metadata headers.
        /// </summary>
        public IReadOnlyDictionary<string, object> Headers => Source.Headers;

        /// <summary>
        /// Gets the correlation id when present in headers.
        /// </summary>
        public string CorrelationId => Source.CorrelationId;

        /// <summary>
        /// Gets the number of dispatch failures recorded for this message.
        /// </summary>
        public int RetryAttempts => Source.RetryAttempts;

        /// <summary>
        /// Gets the failure exception, when the message failed.
        /// </summary>
        public Exception Exception => Source.Exception;
    }
}
