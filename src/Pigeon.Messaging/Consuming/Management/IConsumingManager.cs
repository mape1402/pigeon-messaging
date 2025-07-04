namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Defines the contract for the consuming manager, which orchestrates
    /// the lifecycle of one or more <see cref="IMessageBrokerAdapter"/> instances,
    /// starts the consuming loop, and dispatches consumed messages
    /// to the configured consumers.
    /// </summary>
    public interface IConsumingManager
    {
        /// <summary>
        /// Starts the consuming process across all configured message broker adapters.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that can be used to signal cancellation and gracefully stop consumption.
        /// </param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops the consuming process across all configured message broker adapters,
        /// unsubscribes from events, and gracefully closes any active connections.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token that can be used to signal cancellation and enforce a graceful shutdown.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> that represents the asynchronous stop operation.
        /// </returns>
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
