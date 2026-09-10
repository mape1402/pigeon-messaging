namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Publishes prepared Pigeon envelopes without rebuilding the original producer pipeline.
    /// </summary>
    public interface IPigeonPublisherInvoker
    {
        /// <summary>
        /// Publishes a prepared envelope to the configured broker adapter.
        /// </summary>
        /// <param name="envelope">The prepared publish envelope.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task that completes when the envelope has been published.</returns>
        ValueTask PublishAsync(
            PigeonPublishEnvelope envelope,
            CancellationToken cancellationToken = default);
    }
}
