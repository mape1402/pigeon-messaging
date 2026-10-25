namespace Pigeon.Messaging.Tests.Consuming.Configuration;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;

public class ConsumerScannerAssemblyTests
{
    [Fact]
    public void Constructor_RequiresServices()
        => Assert.Throws<ArgumentNullException>(() => new ConsumerScanner(null, Substitute.For<IConsumingConfigurator>()));

    [Fact]
    public void Constructor_RequiresConfigurator()
        => Assert.Throws<ArgumentNullException>(() => new ConsumerScanner(new ServiceCollection(), null));
    [Fact]
    public void Scan_ProcessesAssemblyWithoutConcreteConsumers()
    {
        var services = new ServiceCollection();
        var configurator = Substitute.For<IConsumingConfigurator>();
        new ConsumerScanner(services, configurator).Scan(typeof(HubConsumer).Assembly);
        Assert.Empty(services);
    }

    [Fact]
    public void ScanHubConsumers_IgnoresUnrelatedAndAbstractTypes()
    {
        var services = new ServiceCollection();
        new ConsumerScanner(services, Substitute.For<IConsumingConfigurator>()).ScanHubConsumers(new[] { typeof(string), typeof(HubConsumer) });
        Assert.Empty(services);
    }
}
