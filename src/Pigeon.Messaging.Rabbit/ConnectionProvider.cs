namespace Pigeon.Messaging.Rabbit
{
    using Microsoft.Extensions.Options;
    using RabbitMQ.Client;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides RabbitMQ connection and channel management with safe async initialization and disposal.
    /// Maintains a singleton <see cref="IConnection"/> instance reused across channels.
    /// </summary>
    internal class ConnectionProvider : IConnectionProvider, IAsyncDisposable
    {
        private readonly RabbitSettings _options;
        private IConnection _connection;

        // Semaphore to ensure only one concurrent connection creation operation
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private readonly IConnectionFactory _factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionProvider"/> class.
        /// </summary>
        /// <param name="factory">
        /// The <see cref="IConnectionFactory"/> used to create RabbitMQ connections.
        /// </param>
        /// <param name="options">
        /// The RabbitMQ configuration settings injected via the options pattern.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="factory"/> or <paramref name="options"/> is null.
        /// </exception>
        public ConnectionProvider(IConnectionFactory factory, IOptions<RabbitSettings> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Creates or returns an open RabbitMQ connection asynchronously.
        /// Ensures thread-safe lazy initialization using a semaphore.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes with an open <see cref="IConnection"/>.</returns>
        public async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection == null || !_connection.IsOpen)
                {
                    _factory.Uri = new Uri(_options.Url);
                    _connection = await _factory.CreateConnectionAsync(cancellationToken);
                }
                return _connection;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Creates a new channel on the current open connection.
        /// If the connection is not open, it will be created first.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task that completes with a new <see cref="IChannel"/> instance.</returns>
        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (_connection == null || !_connection.IsOpen)
                await CreateConnectionAsync(cancellationToken);

            return await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Asynchronously disposes the RabbitMQ connection by closing it if open and releasing resources.
        /// </summary>
        /// <returns>A task representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_connection != null)
            {
                if (_connection.IsOpen)
                    await _connection.CloseAsync();

                _connection.Dispose();
                _connection = null;
            }
        }
    }
}
