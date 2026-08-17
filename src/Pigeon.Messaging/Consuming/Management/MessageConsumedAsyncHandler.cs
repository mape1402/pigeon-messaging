namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Handles consumed broker messages asynchronously.
    /// </summary>
    /// <param name="sender">The adapter that received the message.</param>
    /// <param name="args">The consumed message details.</param>
    /// <param name="cancellationToken">A cancellation token for async backpressure.</param>
    /// <returns>A task-like value that completes when Pigeon accepted the message.</returns>
    public delegate ValueTask MessageConsumedAsyncHandler(
        object sender,
        MessageConsumedEventArgs args,
        CancellationToken cancellationToken = default);
}
