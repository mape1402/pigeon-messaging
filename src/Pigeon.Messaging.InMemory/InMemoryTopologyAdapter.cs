namespace Pigeon.Messaging.InMemory
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    internal sealed class InMemoryTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        public string BrokerName => "InMemory";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
