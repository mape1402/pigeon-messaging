namespace Pigeon.Messaging.Azure.ServiceBus
{
    using global::Azure;
    using global::Azure.Messaging.ServiceBus;
    using global::Azure.Messaging.ServiceBus.Administration;
    using Microsoft.Extensions.Options;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    internal class ServiceBusTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        private readonly ServiceBusAdministrationClient _adminClient;

        public ServiceBusTopologyAdapter(IOptions<AzureServiceBusSettings> options)
        {
            if (options?.Value == null)
                throw new ArgumentNullException(nameof(options));

            _adminClient = new ServiceBusAdministrationClient(options.Value.ConnectionString);
        }

        internal ServiceBusTopologyAdapter(ServiceBusAdministrationClient adminClient)
        {
            _adminClient = adminClient ?? throw new ArgumentNullException(nameof(adminClient));
        }

        public string BrokerName => "AzureServiceBus";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => EnsureQueueAsync(route.Topic, cancellationToken);

        public async Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
        {
            if (endpoint.Subscription == ConsumerEndpoint.DefaultSubscription)
            {
                await EnsureQueueAsync(endpoint.Topic, cancellationToken);
                return;
            }

            await EnsureTopicAsync(endpoint.Topic, cancellationToken);

            await IgnoreAlreadyExistsAsync(
                () => _adminClient.CreateSubscriptionAsync(endpoint.Topic, endpoint.Subscription, cancellationToken));
        }

        private Task EnsureQueueAsync(string queue, CancellationToken cancellationToken)
            => IgnoreAlreadyExistsAsync(() => _adminClient.CreateQueueAsync(queue, cancellationToken));

        private Task EnsureTopicAsync(string topic, CancellationToken cancellationToken)
            => IgnoreAlreadyExistsAsync(() => _adminClient.CreateTopicAsync(topic, cancellationToken));

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
