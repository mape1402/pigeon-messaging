namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Topology;

public class EventGridConsumingValidationTests
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
        var provider = missing == 1 ? null : Substitute.For<IEventGridProvider>();
        var topology = missing == 2 ? null : Substitute.For<ITopologyProvisioningService>();
        var settings = missing == 3 ? null : Options.Create(new GlobalSettings());
        var logger = missing == 4 ? null : NullLogger<EventGridConsumingAdapter>.Instance;
        Assert.Throws<ArgumentNullException>(() => new EventGridConsumingAdapter(configurator, provider, topology, settings, logger));
    }
}
