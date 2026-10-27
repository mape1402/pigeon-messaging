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
        private readonly AzureEventGridClientFactory _clientFactory;
        private readonly Func<ServiceBusAdministrationClient> _createAdminClient;

        internal EventGridTopologyAdapter(IOptions<AzureEventGridSettings> options)
            : this(new AzureEventGridClientFactory(options))
        {
        }

        public EventGridTopologyAdapter(AzureEventGridClientFactory clientFactory)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _createAdminClient = _clientFactory.CreateServiceBusAdministrationClient;
        }

        internal EventGridTopologyAdapter(AzureEventGridClientFactory clientFactory, Func<ServiceBusAdministrationClient> createAdminClient)
        {
            _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
            _createAdminClient = createAdminClient ?? throw new ArgumentNullException(nameof(createAdminClient));
        }

        public string BrokerName => "AzureEventGrid";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public async Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            if (!_clientFactory.HasServiceBusTransport)
                return;

            var adminClient = _createAdminClient();

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
