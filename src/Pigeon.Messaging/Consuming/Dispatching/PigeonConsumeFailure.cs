namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Optional failure details supplied by the decision interceptor.</summary>
    public sealed record PigeonConsumeFailure
    {
        /// <summary>Gets the failure code.</summary>
        public string Code { get; init; }
        /// <summary>Gets the failure type.</summary>
        public string Type { get; init; }
        /// <summary>Gets the failure message.</summary>
        public string Message { get; init; }
        /// <summary>Gets whether the failure can be retried, when known.</summary>
        public bool? IsRetryable { get; init; }
    }
}
