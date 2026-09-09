namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Represents the next step in the consume execution pipeline.
    /// </summary>
    /// <param name="context">The current consume context.</param>
    /// <param name="cancellationToken">A token for cooperative cancellation.</param>
    /// <returns>A task that represents the asynchronous consume execution.</returns>
    public delegate ValueTask ConsumeExecutionDelegate(
        ConsumeContext context,
        CancellationToken cancellationToken = default);
}
