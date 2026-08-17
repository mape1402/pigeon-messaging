namespace Pigeon.Messaging.Rabbit.Producing
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using RabbitMQ.Client;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Adapter implementation to publish messages to RabbitMQ using the new IChannel API.
    /// This class manages a pool of channels and ensures thread-safe access per channel.
    /// </summary>
    internal class RabbitProducingAdapter : IMessageBrokerProducingAdapter, IAsyncDisposable
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly ISerializer _serializer;
        private readonly RabbitSettings _settings;
        private readonly ILogger<RabbitProducingAdapter> _logger;
        private readonly RabbitPublisherChannel[] _channels;

        private int _nextChannelIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitProducingAdapter"/> class.
        /// </summary>
        /// <param name="connectionProvider">Provider for RabbitMQ connections and channels.</param>
        /// <param name="serializer">Serializer for converting messages to JSON format.</param>
        /// <param name="settings">RabbitMQ adapter settings.</param>
        /// <param name="logger">Logger instance for error and info logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public RabbitProducingAdapter(IConnectionProvider connectionProvider, ISerializer serializer, IOptions<RabbitSettings> settings, ILogger<RabbitProducingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _channels = Enumerable.Range(0, GetPublisherChannelPoolSize())
                .Select(index => new RabbitPublisherChannel(index))
                .ToArray();
        }

        /// <summary>
        /// Publishes a wrapped message payload asynchronously to the specified RabbitMQ topic (queue).
        /// Ensures the channel is open and the topic is declared before publishing.
        /// Thread-safe to handle concurrent publish calls using an async semaphore.
        /// </summary>
        /// <typeparam name="T">Type of the message payload.</typeparam>
        /// <param name="payload">The wrapped payload containing message and metadata.</param>
        /// <param name="topic">The RabbitMQ topic (queue name) to publish the message to.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous publish operation.</returns>
        /// <exception cref="Exception">Any exception during publishing is logged and rethrown.</exception>
        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, string topic, CancellationToken cancellationToken = default)
            where T : class
            => await PublishMessageAsync(payload, PublishingRoute.ForTopic(topic), cancellationToken);

        public async ValueTask PublishMessageAsync<T>(WrappedPayload<T> payload, PublishingRoute route, CancellationToken cancellationToken = default)
            where T : class
            => await PublishCoreAsync(payload, route, cancellationToken);

        public async ValueTask PublishRawMessageAsync<T>(T message, string topic, CancellationToken cancellationToken = default)
            where T : class
            => await PublishRawMessageAsync(message, PublishingRoute.ForTopic(topic), cancellationToken);

        public async ValueTask PublishRawMessageAsync<T>(T message, PublishingRoute route, CancellationToken cancellationToken = default)
            where T : class
            => await PublishCoreAsync(message, route, cancellationToken);

        private async ValueTask PublishCoreAsync(object payload, PublishingRoute route, CancellationToken cancellationToken = default)
        {
            var publisherChannel = GetNextPublisherChannel();
            await publisherChannel.Lock.WaitAsync(cancellationToken);

            try
            {
                if (publisherChannel.Channel == null || !publisherChannel.Channel.IsOpen)
                    publisherChannel.Channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

                var exchange = ResolveExchange(route);

                var body = _serializer.SerializeAsBytes(payload);

                await publisherChannel.Channel.BasicPublishAsync(exchange, route.RoutingKey, false, new BasicProperties(), body,  cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing message using Rabbit Adapter channel {ChannelIndex}.", publisherChannel.Index);
                throw;
            }
            finally
            {
                publisherChannel.Lock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var publisherChannel in _channels)
            {
                await publisherChannel.Lock.WaitAsync();

                try
                {
                    if (publisherChannel.Channel != null)
                        await publisherChannel.Channel.DisposeAsync();
                }
                finally
                {
                    publisherChannel.Lock.Release();
                    publisherChannel.Lock.Dispose();
                }
            }
        }

        private string ResolveExchange(PublishingRoute route)
            => !string.IsNullOrWhiteSpace(route.Exchange)
                ? route.Exchange
                : _settings.Exchange ?? string.Empty;

        private RabbitPublisherChannel GetNextPublisherChannel()
        {
            var index = Interlocked.Increment(ref _nextChannelIndex);
            return _channels[Math.Abs(index % _channels.Length)];
        }

        private int GetPublisherChannelPoolSize()
            => _settings.PublisherChannelPoolSize > 0
                ? _settings.PublisherChannelPoolSize
                : Math.Max(4, Environment.ProcessorCount * 2);

        private sealed class RabbitPublisherChannel
        {
            public RabbitPublisherChannel(int index)
            {
                Index = index;
            }

            public int Index { get; }

            public IChannel Channel { get; set; }

            public SemaphoreSlim Lock { get; } = new(1, 1);
        }
    }
}
