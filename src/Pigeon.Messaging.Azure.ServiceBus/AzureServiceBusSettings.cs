namespace Pigeon.Messaging.Azure.ServiceBus
{
    using global::Azure.Core;

    /// <summary>
    /// Represents the configuration settings required to connect to Azure Service Bus.
    /// </summary>
    public class AzureServiceBusSettings
    {
        /// <summary>
        /// Gets or sets the connection string used to connect to Azure Service Bus.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the fully qualified Service Bus namespace used with <see cref="Credential"/>.
        /// </summary>
        public string FullyQualifiedNamespace { get; set; }

        /// <summary>
        /// Gets or sets the programmatically supplied Microsoft Entra credential.
        /// </summary>
        /// <remarks>
        /// This property cannot be bound from application configuration. Configure it in code.
        /// </remarks>
        public TokenCredential Credential { get; set; }
    }
}
