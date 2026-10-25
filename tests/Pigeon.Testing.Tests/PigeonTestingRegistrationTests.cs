namespace Pigeon.Testing.Tests;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class PigeonTestingRegistrationTests
{
    [Fact]
    public void AddTesting_RequiresServices()
        => Assert.Throws<ArgumentNullException>(() => PigeonTestingServiceCollectionExtensions.AddPigeonTesting(null));

    [Fact]
    public void AddConsumers_RequiresServices()
        => Assert.Throws<ArgumentNullException>(() => PigeonTestingServiceCollectionExtensions.AddPigeonTestingConsumers(null));

    [Fact]
    public void AddTesting_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddPigeonTesting().AddPigeonTesting();
        Assert.Single(services.Where(x => x.ServiceType == typeof(IPigeonTestingTransport)));
    }

    [Fact]
    public void AddConsumers_RequiresConfiguredConsumerRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IPigeonTestingTransport>());
        Assert.Throws<InvalidOperationException>(() => services.AddPigeonTestingConsumers());
    }

    [Fact]
    public void AddAdapter_RegistersTransport()
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddPigeonTestingAdapter());
        Assert.Contains(services, x => x.ServiceType == typeof(IPigeonTestingTransport));
    }
}
