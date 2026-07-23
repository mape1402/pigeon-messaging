namespace Pigeon.Messaging.Topology
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Provides broker-specific topology provisioning operations.
    /// </summary>
    public interface IMessageBrokerTopologyAdapter
    {
        /// <summary>
        /// Gets the broker name used in provisioning cache keys and diagnostics.
        /// </summary>
        string BrokerName { get; }

        /// <summary>
        /// Ensures the broker topology required to publish to a route exists.
        /// </summary>
        Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures the broker topology required to consume from an endpoint exists.
        /// </summary>
        Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default);
    }
}
