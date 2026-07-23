namespace Pigeon.Messaging.Azure.EventHub
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    internal class EventHubTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        public string BrokerName => "AzureEventHub";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
