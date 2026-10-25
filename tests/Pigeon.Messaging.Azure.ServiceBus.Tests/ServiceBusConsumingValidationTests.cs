namespace Pigeon.Messaging.Azure.ServiceBus.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Topology;

public class ServiceBusConsumingValidationTests
{
    [Theory]
    [InlineData(0, "consumingConfigurator")]
    [InlineData(1, "serviceBusProvider")]
    [InlineData(2, "topologyProvisioningService")]
    [InlineData(3, "globalSettings")]
    [InlineData(4, "logger")]
    public void Constructor_RequiresEachDependency(int missing, string parameter)
    {
        var options = Substitute.For<IOptions<GlobalSettings>>();
        options.Value.Returns(missing == 3 ? null : new GlobalSettings());
        var exception = Assert.Throws<ArgumentNullException>(() => new ServiceBusConsumingAdapter(
            missing == 0 ? null : Substitute.For<IConsumingConfigurator>(),
            missing == 1 ? null : Substitute.For<IServiceBusProvider>(),
            missing == 2 ? null : Substitute.For<ITopologyProvisioningService>(),
            options,
            missing == 4 ? null : NullLogger<ServiceBusConsumingAdapter>.Instance));
        Assert.Equal(parameter, exception.ParamName);
    }
}
