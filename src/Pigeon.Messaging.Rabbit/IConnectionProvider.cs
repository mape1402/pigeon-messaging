namespace Pigeon.Messaging.Rabbit
{
    using RabbitMQ.Client;

    /// <summary>
    /// Defines a contract for providing a shared RabbitMQ connection and creating channels.
    /// </summary>
    public interface IConnectionProvider
    {
        /// <summary>
        /// Creates or retrieves a long-lived RabbitMQ connection.
        /// Should return the same connection if already open.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The active <see cref="IConnection"/>.</returns>
        Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a new logical channel (IModel) for communicating with RabbitMQ.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A new <see cref="IModel"/> instance.</returns>
        Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default);
    }
}
