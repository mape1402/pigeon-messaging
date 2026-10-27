namespace Pigeon.Messaging.Azure.ServiceBus
{
    using global::Azure.Messaging.ServiceBus;
    using global::Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Options;

    internal sealed class AzureServiceBusClientFactory
    {
        private readonly AzureServiceBusSettings _settings;

        public AzureServiceBusClientFactory(IOptions<AzureServiceBusSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            AzureServiceBusSettingsValidator.Validate(_settings);
        }

        public ServiceBusClient CreateMessagingClient()
            => HasConnectionString
                ? new ServiceBusClient(_settings.ConnectionString)
                : new ServiceBusClient(_settings.FullyQualifiedNamespace, _settings.Credential);

        public ServiceBusAdministrationClient CreateAdministrationClient()
            => HasConnectionString
                ? new ServiceBusAdministrationClient(_settings.ConnectionString)
                : new ServiceBusAdministrationClient(_settings.FullyQualifiedNamespace, _settings.Credential);

        private bool HasConnectionString
            => !string.IsNullOrWhiteSpace(_settings.ConnectionString);
    }
}
