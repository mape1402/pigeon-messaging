namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Defines an interceptor that wraps the execution of a consumer handler.
    /// </summary>
    public interface IConsumeExecutionInterceptor
    {
        /// <summary>
        /// Invokes logic around the next consume execution step.
        /// </summary>
        /// <param name="context">The current consume context.</param>
        /// <param name="next">The next step in the consume execution pipeline.</param>
        /// <param name="cancellationToken">A token for cooperative cancellation.</param>
        /// <returns>A task that represents the asynchronous consume execution.</returns>
        ValueTask InvokeAsync(
            ConsumeContext context,
            ConsumeExecutionDelegate next,
            CancellationToken cancellationToken = default);
    }
}
