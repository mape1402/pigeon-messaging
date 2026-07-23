namespace Pigeon.Messaging.Topology
{
    using Microsoft.Extensions.Hosting;

    internal class TopologyProvisioningHostedService : IHostedService
    {
        private readonly ITopologyProvisioningService _topologyProvisioningService;

        public TopologyProvisioningHostedService(ITopologyProvisioningService topologyProvisioningService)
        {
            _topologyProvisioningService = topologyProvisioningService ?? throw new ArgumentNullException(nameof(topologyProvisioningService));
        }

        public Task StartAsync(CancellationToken cancellationToken)
            => _topologyProvisioningService.EnsureStartupTopologyAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
