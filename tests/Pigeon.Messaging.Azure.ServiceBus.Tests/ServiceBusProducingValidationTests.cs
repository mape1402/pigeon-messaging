namespace Pigeon.Messaging.Azure.ServiceBus.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Producing;

public class ServiceBusProducingValidationTests
{
    [Fact]
    public void Constructor_RequiresProvider()
        => Assert.Throws<ArgumentNullException>(() => new ServiceBusProducingAdapter(null, Substitute.For<ISerializer>(), NullLogger<ServiceBusProducingAdapter>.Instance));

    [Fact]
    public void Constructor_RequiresLogger()
        => Assert.Throws<ArgumentNullException>(() => new ServiceBusProducingAdapter(Substitute.For<IServiceBusProvider>(), Substitute.For<ISerializer>(), null));
}
