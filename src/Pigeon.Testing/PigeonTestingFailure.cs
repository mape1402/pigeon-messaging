namespace Pigeon.Testing
{
    /// <summary>
    /// Represents a failure recorded by the testing transport.
    /// </summary>
    public sealed class PigeonTestingFailure
    {
        internal PigeonTestingFailure(PigeonTestingMessage message, Exception exception, PigeonTestingFailureKind kind)
        {
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Kind = kind;
        }

        /// <summary>
        /// Gets the failed testing message.
        /// </summary>
        public PigeonTestingMessage Message { get; }

        /// <summary>
        /// Gets the recorded exception.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the failure kind.
        /// </summary>
        public PigeonTestingFailureKind Kind { get; }
    }
}
