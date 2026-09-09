namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Defines a publish interceptor that can explicitly continue, skip, reject, use outbox, or publish immediately.
    /// </summary>
    public interface IPublishDecisionInterceptor
    {
        /// <summary>
        /// Inspects the current publish context and returns a pipeline decision.
        /// </summary>
        /// <param name="context">The publish context for the current message.</param>
        /// <param name="cancellationToken">A token for cooperative cancellation.</param>
        /// <returns>The selected publish decision.</returns>
        ValueTask<PigeonPublishDecisionResult> InterceptAsync(
            PublishContext context,
            CancellationToken cancellationToken = default);
    }
}
