namespace Pigeon.Testing
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    internal sealed class PigeonTestingTopologyAdapter : IMessageBrokerTopologyAdapter
    {
        public string BrokerName => "PigeonTesting";

        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
