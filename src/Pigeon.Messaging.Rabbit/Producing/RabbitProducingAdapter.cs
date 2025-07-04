namespace Pigeon.Messaging.Rabbit.Producing
{
    using Microsoft.Extensions.Logging;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing.Management;
    using RabbitMQ.Client;
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Adapter implementation to publish messages to RabbitMQ using the new IChannel API.
    /// This class manages a single channel and ensures thread-safe access for publishing.
    /// </summary>
    internal class RabbitProducingAdapter : IMessageBrokerProducingAdapter
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly ILogger<RabbitProducingAdapter> _logger;

        // Tracks topics that have already been declared to avoid redundant declarations
        private readonly ConcurrentDictionary<string, byte> _registeredTopics = new();

        // Single channel instance reused for publishing
        private IChannel _channel;

        // SemaphoreSlim to provide async thread-safe access to the channel
        private readonly SemaphoreSlim _channelLock = new(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="RabbitProducingAdapter"/> class.
        /// </summary>
        /// <param name="connectionProvider">Provider for RabbitMQ connections and channels.</param>
        /// <param name="logger">Logger instance for error and info logging.</param>
        /// <exception cref="ArgumentNullException">Thrown if any dependency is null.</exception>
        public RabbitProducingAdapter(IConnectionProvider connectionProvider, ILogger<RabbitProducingAdapter> logger)
        {
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        {
            // Acquire the semaphore to ensure exclusive access to the channel for publishing
            await _channelLock.WaitAsync(cancellationToken);

            try
            {
                // Create or reuse the channel if it's closed
                if (_channel == null || !_channel.IsOpen)
                    _channel = await _connectionProvider.CreateChannelAsync(cancellationToken);

                // Declare the topic (queue) once to avoid redundant declarations
                if (_registeredTopics.TryAdd(topic, 0))
                    await _channel.QueueDeclareAsync(topic, durable: false, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

                // Serialize the payload to JSON and encode as UTF-8 bytes
                var payloadJson = JsonSerializer.Serialize(payload);
                var body = Encoding.UTF8.GetBytes(payloadJson);

                // Publish the message to the topic with default exchange (empty string)
                await _channel.BasicPublishAsync(string.Empty, topic, body, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log any exceptions and rethrow
                _logger.LogError(ex, "Error while publishing message using Rabbit Adapter.");
                throw;
            }
            finally
            {
                // Release the semaphore to allow other concurrent calls to publish
                _channelLock.Release();
            }
        }
    }
}