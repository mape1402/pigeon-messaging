namespace Pigeon.Messaging.Azure.EventHub
{
    using global::Azure.Messaging.EventHubs.Consumer;
    using global::Azure.Messaging.EventHubs.Producer;
    using Microsoft.Extensions.Options;

    internal sealed class AzureEventHubClientFactory
    {
        private readonly AzureEventHubSettings _settings;

        public AzureEventHubClientFactory(IOptions<AzureEventHubSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            AzureEventHubSettingsValidator.Validate(_settings);
        }

        public EventHubProducerClient CreateProducer(string hubName)
            => HasConnectionString
                ? new EventHubProducerClient(_settings.ConnectionString, hubName)
                : new EventHubProducerClient(_settings.FullyQualifiedNamespace, hubName, _settings.Credential);

        public EventHubConsumerClient CreateConsumer(string consumerGroup, string hubName)
            => HasConnectionString
                ? new EventHubConsumerClient(consumerGroup, _settings.ConnectionString, hubName)
                : new EventHubConsumerClient(consumerGroup, _settings.FullyQualifiedNamespace, hubName, _settings.Credential);

        private bool HasConnectionString
            => !string.IsNullOrWhiteSpace(_settings.ConnectionString);
    }
}
