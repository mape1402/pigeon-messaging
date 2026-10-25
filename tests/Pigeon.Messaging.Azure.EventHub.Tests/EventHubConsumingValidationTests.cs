namespace Pigeon.Messaging.Azure.EventHub.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Topology;

public class EventHubConsumingValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Constructor_RequiresEachDependency(int missing)
    {
        var configurator = missing == 0 ? null : Substitute.For<IConsumingConfigurator>();
        var provider = missing == 1 ? null : Substitute.For<IEventHubProvider>();
        var topology = missing == 2 ? null : Substitute.For<ITopologyProvisioningService>();
        var settings = missing == 3 ? null : Options.Create(new GlobalSettings());
        var logger = missing == 4 ? null : NullLogger<EventHubConsumingAdapter>.Instance;
        Assert.Throws<ArgumentNullException>(() => new EventHubConsumingAdapter(configurator, provider, topology, settings, logger));
    }
}
