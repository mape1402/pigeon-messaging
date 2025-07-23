namespace Pigeon.Messaging.Azure.ServiceBus
{
    using global::Azure.Messaging.ServiceBus;

    /// <summary>
    /// Provides methods to interact with Azure Service Bus, allowing the creation of clients, senders, and processors for messaging operations.
    /// </summary>
    /// <remarks>
    /// This interface defines methods to obtain a Service Bus client, a sender for a specified queue or topic, and a processor for a topic.
    /// Implementations of this interface should handle the creation and management of these Service Bus entities.
    /// </remarks>
    public interface IServiceBusProvider
    {
        /// <summary>
        /// Gets a Service Bus client for the specified connection string.
        /// </summary>
        /// <returns>A Service Bus client.</returns>
        ServiceBusClient GetClient();

        /// <summary>
        /// Gets a Service Bus sender for the specified queue or topic.
        /// </summary>
        /// <param name="topic">The name of the queue or topic.</param>
        /// <returns>A Service Bus sender.</returns>
        ServiceBusSender GetSender(string topic);

        /// <summary>
        /// Creates a Service Bus processor for the specified topic.
        /// </summary>
        /// <param name="topic">The name of the topic.</param>
        /// <returns>A Service Bus processor.</returns>
        ServiceBusProcessor CreateProcessor(string topic);
    }
}
