namespace Pigeon.Messaging.Azure.EventHub
{
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Producer;
    using Microsoft.Extensions.Options;
    using System.Collections.Concurrent;

    /// <summary>
    /// Provides methods to interact with Azure Event Hubs, allowing the creation of producers and processors for messaging operations.
    /// </summary>
    public interface IEventHubProvider
    {
        /// <summary>
        /// Gets an Event Hub producer for the specified hub name.
        /// </summary>
        /// <param name="hubName">The name of the event hub.</param>
        /// <returns>An Event Hub producer.</returns>
        EventHubProducerClient GetProducer(string hubName);

        /// <summary>
        /// Creates an Event Hub processor for the specified hub name.
        /// </summary>
        /// <param name="hubName">The name of the event hub.</param>
        /// <returns>An Event Hub processor.</returns>
        IEventHubProcessor CreateProcessor(string hubName);
    }

    /// <summary>
    /// Defines a contract for processing events from Event Hubs.
    /// </summary>
    public interface IEventHubProcessor : IDisposable
    {
        /// <summary>
        /// Reads events asynchronously from the Event Hub.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for events.</param>
        /// <returns>An async enumerable of partition events.</returns>
        IAsyncEnumerable<PartitionEvent> ReadEventsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provides methods to interact with Azure Event Hubs, allowing the creation of producers and processors for messaging operations.
    /// </summary>
    internal class EventHubProvider : IEventHubProvider
    {
        private readonly AzureEventHubSettings _settings;
        private readonly ConcurrentDictionary<string, EventHubProducerClient> _producers = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventHubProvider"/> class.
        /// </summary>
        /// <param name="options">The Azure Event Hub settings options.</param>
        public EventHubProvider(IOptions<AzureEventHubSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public EventHubProducerClient GetProducer(string hubName)
        {
            return _producers.GetOrAdd(hubName, name => new EventHubProducerClient(_settings.ConnectionString, name));
        }

        /// <inheritdoc />
        public IEventHubProcessor CreateProcessor(string hubName)
        {
            return new EventHubProcessor(_settings, hubName);
        }
    }

    /// <summary>
    /// Event Hub processor implementation.
    /// </summary>
    internal class EventHubProcessor : IEventHubProcessor
    {
        private readonly AzureEventHubSettings _settings;
        private readonly string _hubName;
        private EventHubConsumerClient _consumerClient;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public EventHubProcessor(AzureEventHubSettings settings, string hubName)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _hubName = hubName ?? throw new ArgumentNullException(nameof(hubName));
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async IAsyncEnumerable<PartitionEvent> ReadEventsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Initialize consumer client if not already done
            if (_consumerClient == null)
            {
                _consumerClient = new EventHubConsumerClient(_settings.ConsumerGroup, _settings.ConnectionString, _hubName);
            }

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationTokenSource.Token).Token;

            try
            {
                // Get partition information
                var partitionIds = await _consumerClient.GetPartitionIdsAsync(linkedToken);

                // Read from all partitions
                var partitionTasks = partitionIds.Select(partitionId => 
                    ReadFromPartitionAsync(partitionId, linkedToken)).ToArray();

                // Merge events from all partitions
                await foreach (var partitionEvent in MergePartitionEvents(partitionTasks, linkedToken))
                {
                    yield return partitionEvent;
                }
            }
            finally
            {
                // Consumer client will be disposed in Dispose method
            }
        }

        private async IAsyncEnumerable<PartitionEvent> ReadFromPartitionAsync(string partitionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            IAsyncEnumerator<PartitionEvent> enumerator = null;
            try
            {
                enumerator = _consumerClient.ReadEventsFromPartitionAsync(
                    partitionId,
                    EventPosition.Latest,
                    cancellationToken).GetAsyncEnumerator(cancellationToken);

                while (true)
                {
                    PartitionEvent currentEvent;
                    try
                    {
                        if (!await enumerator.MoveNextAsync())
                        {
                            break;
                        }
                        currentEvent = enumerator.Current;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancellation is requested
                        break;
                    }
                    catch (Exception)
                    {
                        // In production, log the error and handle appropriately
                        break;
                    }

                    if (currentEvent.Data != null)
                    {
                        yield return currentEvent;
                    }
                }
            }
            finally
            {
                if (enumerator != null)
                {
                    await enumerator.DisposeAsync();
                }
            }
        }

        private static async IAsyncEnumerable<PartitionEvent> MergePartitionEvents(
            IAsyncEnumerable<PartitionEvent>[] partitionEnumerables,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var enumerators = partitionEnumerables.Select(e => e.GetAsyncEnumerator(cancellationToken)).ToArray();
            var activeTasks = new List<Task<(int Index, bool HasValue, PartitionEvent Event)>>();

            try
            {
                // Initialize tasks for each partition
                for (int i = 0; i < enumerators.Length; i++)
                {
                    int index = i; // Capture for closure
                    activeTasks.Add(GetNextEventAsync(enumerators[index], index));
                }

                while (activeTasks.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var completedTask = await Task.WhenAny(activeTasks);
                    activeTasks.Remove(completedTask);

                    var (index, hasValue, partitionEvent) = await completedTask;

                    if (hasValue)
                    {
                        yield return partitionEvent;

                        // Start a new task for this enumerator
                        activeTasks.Add(GetNextEventAsync(enumerators[index], index));
                    }
                    // If hasValue is false, this enumerator is done, don't add it back
                }
            }
            finally
            {
                // Dispose all enumerators
                foreach (var enumerator in enumerators)
                {
                    await enumerator.DisposeAsync();
                }
            }
        }

        private static async Task<(int Index, bool HasValue, PartitionEvent Event)> GetNextEventAsync(
            IAsyncEnumerator<PartitionEvent> enumerator, int index)
        {
            try
            {
                bool hasValue = await enumerator.MoveNextAsync();
                return (index, hasValue, hasValue ? enumerator.Current : default);
            }
            catch
            {
                return (index, false, default);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _consumerClient?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(30));
            _cancellationTokenSource?.Dispose();
        }
    }
}