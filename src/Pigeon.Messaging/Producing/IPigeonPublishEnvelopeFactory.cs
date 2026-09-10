namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Creates durable publish envelopes from prepared Pigeon publish contexts.
    /// </summary>
    public interface IPigeonPublishEnvelopeFactory
    {
        /// <summary>
        /// Creates a durable envelope from the current publish context.
        /// </summary>
        /// <param name="context">The prepared publish context.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>The durable publish envelope.</returns>
        ValueTask<PigeonPublishEnvelope> CreateAsync(
            PublishContext context,
            CancellationToken cancellationToken = default);
    }
}
