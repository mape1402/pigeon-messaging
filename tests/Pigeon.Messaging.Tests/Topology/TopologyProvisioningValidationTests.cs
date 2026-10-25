namespace Pigeon.Messaging.Tests.Topology;

using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Topology;

public class TopologyProvisioningValidationTests
{
    [Theory]
    [InlineData(0, "adapters")]
    [InlineData(1, "consumingConfigurator")]
    [InlineData(2, "settings")]
    public void Constructor_RequiresEachDependency(int missing, string parameter)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new TopologyProvisioningService(
            missing == 0 ? null : Array.Empty<IMessageBrokerTopologyAdapter>(),
            missing == 1 ? null : Substitute.For<IConsumingConfigurator>(),
            missing == 2 ? null : Options.Create(new GlobalSettings())));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public async Task StartupDisabled_DoesNotInspectEndpoints()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        var service = new TopologyProvisioningService(Array.Empty<IMessageBrokerTopologyAdapter>(), configurator, Options.Create(new GlobalSettings()));
        await service.EnsureStartupTopologyAsync();
        configurator.DidNotReceive().GetAllEndpoints();
    }

    [Fact]
    public async Task Startup_ProvisionsConfiguredPublishRoutes()
    {
        var adapter = Substitute.For<IMessageBrokerTopologyAdapter>();
        adapter.BrokerName.Returns("test");
        var settings = new GlobalSettings { TopologyProvisioningMode = TopologyProvisioningMode.OnStartup };
        var route = PublishingRoute.ForTopic("orders");
        settings.PublishTopologyRoutes.Add(route);
        var service = new TopologyProvisioningService(new[] { adapter }, Substitute.For<IConsumingConfigurator>(), Options.Create(settings));
        await service.EnsureStartupTopologyAsync();
        await adapter.Received(1).EnsurePublishTopologyAsync(route, Arg.Any<CancellationToken>());
    }
}
