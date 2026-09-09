namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Invokes the Pigeon consumer pipeline from a previously captured consume envelope.
    /// </summary>
    public interface IPigeonConsumerInvoker
    {
        /// <summary>
        /// Replays the envelope through the same consumer pipeline used by live broker deliveries.
        /// </summary>
        /// <param name="envelope">The consume envelope to invoke.</param>
        /// <param name="cancellationToken">A token for cooperative cancellation.</param>
        /// <returns>A task that represents the asynchronous invocation.</returns>
        ValueTask InvokeAsync(
            PigeonConsumeEnvelope envelope,
            CancellationToken cancellationToken = default);
    }
}
