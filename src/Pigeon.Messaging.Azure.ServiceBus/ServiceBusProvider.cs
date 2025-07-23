namespace Pigeon.Messaging.Azure.ServiceBus
{
    using global::Azure.Messaging.ServiceBus;
    using Microsoft.Extensions.Options;
    using System.Collections.Concurrent;

    /// <summary>
    /// Provides methods to interact with Azure Service Bus, allowing the creation of clients, senders, and processors for messaging operations.
    /// </summary>
    internal class ServiceBusProvider : IServiceBusProvider
    {
        private readonly ServiceBusClient _client;
        private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceBusProvider"/> class.
        /// </summary>
        /// <param name="options">The Azure Service Bus settings options.</param>
        public ServiceBusProvider(IOptions<AzureServiceBusSettings> options)
        {
            _client = new ServiceBusClient(options.Value.ConnectionString);
        }
        
        /// <inheritdoc />
        public ServiceBusClient GetClient()
            => _client;

        /// <inheritdoc />
        public ServiceBusSender GetSender(string topic)
        {
            if(_senders.TryGetValue(topic, out var sender))
                return sender;

            sender = _client.CreateSender(topic);

            _senders.TryAdd(topic, sender);

            return sender;
        }

        /// <inheritdoc />
        public ServiceBusProcessor CreateProcessor(string topic)
            => _client.CreateProcessor(topic, new ServiceBusProcessorOptions());
    }
}
