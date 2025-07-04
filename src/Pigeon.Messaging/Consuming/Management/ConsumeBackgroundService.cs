namespace Pigeon.Messaging.Consuming.Management
{
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Represents a hosted background service that starts the consuming manager,
    /// enabling continuous message listening and dispatching to registered consumers.
    /// </summary>
    /// <remarks>
    /// This service typically runs for the lifetime of the application,
    /// orchestrating the connection to the message broker, managing subscriptions,
    /// and executing the consumer pipeline.
    /// </remarks>
    public class ConsumeBackgroundService : IHostedService
    {
        private readonly IConsumingManager _consumingManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsumeBackgroundService"/> class.
        /// </summary>
        /// <param name="consumingManager">
        /// The <see cref="IConsumingManager"/> responsible for starting and managing
        /// the message consumption loop.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="consumingManager"/> is <c>null</c>.
        /// </exception>
        public ConsumeBackgroundService(IConsumingManager consumingManager)
        {
            _consumingManager = consumingManager ?? throw new ArgumentNullException(nameof(consumingManager));
        }

        /// <summary>
        /// Starts the background service, initiating the message consumption process
        /// via the configured <see cref="IConsumingManager"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to signal that start operation should be canceled.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous start operation.</returns>
        public Task StartAsync(CancellationToken cancellationToken)
            => _consumingManager.StartAsync(cancellationToken);

        /// <summary>
        /// Stops the background service, triggering a graceful shutdown
        /// of the message consumption process via the configured <see cref="IConsumingManager"/>.
        /// </summary>
        /// <param name="cancellationToken">
        /// A token to signal that stop operation should be canceled.
        /// </param>
        /// <returns>A <see cref="Task"/> representing the asynchronous stop operation.</returns>
        public Task StopAsync(CancellationToken cancellationToken)
            => _consumingManager.StopAsync(cancellationToken);

    }
}
