namespace Pigeon.Messaging.Tests.Topology;

using Pigeon.Messaging.Topology;
using NSubstitute;

public class TopologyProvisioningHostedServiceValidationTests
{
    [Fact]
    public void Constructor_RequiresProvisioningService() => Assert.Throws<ArgumentNullException>(() => new TopologyProvisioningHostedService(null));

    [Fact]
    public async Task Start_ForwardsCancellationToken()
    {
        var provisioning = Substitute.For<ITopologyProvisioningService>();
        var service = new TopologyProvisioningHostedService(provisioning);
        using var source = new CancellationTokenSource();
        await service.StartAsync(source.Token);
        await provisioning.Received(1).EnsureStartupTopologyAsync(source.Token);
    }
}
