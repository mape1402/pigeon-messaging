namespace Pigeon.Messaging.Rabbit
{
    using Microsoft.Extensions.Options;
    using RabbitMQ.Client;

    internal class ConnectionProvider : IConnectionProvider, IAsyncDisposable
    {
        private readonly RabbitSettings _options;
        private IConnection _connection;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);

        public ConnectionProvider(IOptions<RabbitSettings> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_connection != null && _connection.IsOpen)
                return _connection;

            await _connectionLock.WaitAsync(cancellationToken);
            try
            {
                if (_connection == null || !_connection.IsOpen)
                {
                    var factory = new ConnectionFactory { Uri = new Uri(_options.Url) };
                    _connection = await factory.CreateConnectionAsync(cancellationToken);
                }
                return _connection;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (_connection == null || !_connection.IsOpen)
                await CreateConnectionAsync(cancellationToken);

            return await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        }

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
