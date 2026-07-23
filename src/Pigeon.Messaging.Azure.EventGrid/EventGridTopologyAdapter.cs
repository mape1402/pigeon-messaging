namespace Pigeon.Messaging.Azure.EventGrid
{
    using global::Azure;
    using global::Azure.Messaging.ServiceBus;
    using global::Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    internal class EventGridTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        private readonly AzureEventGridSettings _settings;

        public EventGridTopologyAdapter(IOptions<AzureEventGridSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public string BrokerName => "AzureEventGrid";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_settings.ServiceBusEndPoint))
                return;

            var adminClient = new ServiceBusAdministrationClient(_settings.ServiceBusEndPoint);

            await IgnoreAlreadyExistsAsync(() => adminClient.CreateTopicAsync(endpoint.Topic, cancellationToken));

            if (endpoint.Subscription == ConsumerEndpoint.DefaultSubscription)
                return;

            await IgnoreAlreadyExistsAsync(() => adminClient.CreateSubscriptionAsync(endpoint.Topic, endpoint.Subscription, cancellationToken));
        }

        private static async Task IgnoreAlreadyExistsAsync(Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
            {
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
            }
        }
    }
}
