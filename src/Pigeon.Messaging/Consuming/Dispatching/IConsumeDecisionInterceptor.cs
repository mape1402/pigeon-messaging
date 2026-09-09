namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Defines a consume interceptor that can explicitly continue, skip, retry, reject, or defer a delivery.
    /// </summary>
    public interface IConsumeDecisionInterceptor
    {
        /// <summary>
        /// Inspects the current consume context and returns a pipeline decision.
        /// </summary>
        /// <param name="context">The consume context for the current message.</param>
        /// <param name="cancellationToken">A token for cooperative cancellation.</param>
        /// <returns>The selected consume decision.</returns>
        ValueTask<PigeonConsumeDecisionResult> InterceptAsync(
            ConsumeContext context,
            CancellationToken cancellationToken = default);
    }
}
