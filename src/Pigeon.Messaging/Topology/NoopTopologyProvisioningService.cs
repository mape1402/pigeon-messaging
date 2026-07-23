namespace Pigeon.Messaging.Topology
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// A topology provisioning service that intentionally does not create broker resources.
    /// </summary>
    public sealed class NoopTopologyProvisioningService : ITopologyProvisioningService
    {
        /// <summary>
        /// Gets the shared no-op instance.
        /// </summary>
        public static NoopTopologyProvisioningService Instance { get; } = new();

        private NoopTopologyProvisioningService()
        {
        }

        /// <inheritdoc />
        public Task EnsureStartupTopologyAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task EnsurePublishTopologyAsync(PublishingRoute route, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc />
        public Task EnsureConsumeTopologyAsync(ConsumerEndpoint endpoint, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
