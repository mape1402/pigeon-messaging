namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Creates replayable consume envelopes and reconstructs consume contexts from them.
    /// </summary>
    public interface IPigeonConsumeEnvelopeFactory
    {
        /// <summary>
        /// Captures the current consume context as a serializable envelope.
        /// </summary>
        /// <param name="context">The consume context to capture.</param>
        /// <returns>A replayable consume envelope.</returns>
        PigeonConsumeEnvelope Create(ConsumeContext context);

        /// <summary>
        /// Reconstructs a consume context from a previously captured envelope.
        /// </summary>
        /// <param name="envelope">The envelope to replay.</param>
        /// <param name="services">The scoped services for the replay invocation.</param>
        /// <param name="cancellationToken">A token for cooperative cancellation.</param>
        /// <returns>The reconstructed consume context.</returns>
        ConsumeContext CreateContext(
            PigeonConsumeEnvelope envelope,
            IServiceProvider services,
            CancellationToken cancellationToken = default);
    }
}
