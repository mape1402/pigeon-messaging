namespace Pigeon.Messaging.Topology
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Coordinates topology provisioning according to the configured global policy.
    /// </summary>
    public interface ITopologyProvisioningService
    {
        /// <summary>
        /// Ensures all configured startup topology when enabled.
        /// </summary>
        Task EnsureStartupTopologyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures publish topology for a route when enabled.
        /// </summary>
        Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default);

        /// <summary>
        /// Ensures consume topology for an endpoint when enabled.
        /// </summary>
        Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default);
    }
}
