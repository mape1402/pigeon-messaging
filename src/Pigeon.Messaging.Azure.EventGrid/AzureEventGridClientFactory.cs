namespace Pigeon.Messaging.Azure.EventGrid
{
    using global::Azure;
    using global::Azure.Messaging.EventGrid;
    using global::Azure.Messaging.ServiceBus;
    using global::Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Options;

    internal sealed class AzureEventGridClientFactory
    {
        private readonly AzureEventGridSettings _settings;

        public AzureEventGridClientFactory(IOptions<AzureEventGridSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            AzureEventGridSettingsValidator.Validate(_settings);
        }

        public bool HasServiceBusTransport
            => AzureEventGridSettingsValidator.HasServiceBusTransport(_settings);

        public EventGridPublisherClient CreatePublisher(Endpoint endpoint)
            => !string.IsNullOrWhiteSpace(endpoint.AccessKey)
                ? new EventGridPublisherClient(new Uri(endpoint.Url), new AzureKeyCredential(endpoint.AccessKey))
                : new EventGridPublisherClient(new Uri(endpoint.Url), endpoint.Credential);

        public ServiceBusClient CreateServiceBusClient()
        {
            EnsureServiceBusTransport();
            return !string.IsNullOrWhiteSpace(_settings.ServiceBusEndPoint)
                ? new ServiceBusClient(_settings.ServiceBusEndPoint)
                : new ServiceBusClient(_settings.ServiceBusFullyQualifiedNamespace, _settings.ServiceBusCredential);
        }

        public ServiceBusAdministrationClient CreateServiceBusAdministrationClient()
        {
            EnsureServiceBusTransport();
            return !string.IsNullOrWhiteSpace(_settings.ServiceBusEndPoint)
                ? new ServiceBusAdministrationClient(_settings.ServiceBusEndPoint)
                : new ServiceBusAdministrationClient(_settings.ServiceBusFullyQualifiedNamespace, _settings.ServiceBusCredential);
        }

        private void EnsureServiceBusTransport()
        {
            if (!HasServiceBusTransport)
            {
                throw new InvalidOperationException(
                    "Azure Event Grid consuming requires ServiceBusEndPoint or both ServiceBusFullyQualifiedNamespace and ServiceBusCredential.");
            }
        }
    }
}
