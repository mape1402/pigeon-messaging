namespace Pigeon.Messaging.Tests.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

public class RouteBuilderTests
{
    [Fact]
    public void Registry_RejectsDefaultRouteKey()
        => Assert.Throws<ArgumentException>(() => new PigeonRouteInterceptorRegistry().GetConsumeDecisionInterceptors(default));

    [Fact]
    public void ServiceBuilder_RequiresSettings()
        => Assert.Throws<ArgumentNullException>(() => new PigeonServiceBuilder(null));
    [Fact]
    public void ObserverRegistration_RejectsUnknownFailureBehavior()
    {
        var registry = new PigeonRouteInterceptorRegistry();
        var route = new PigeonRouteKey("orders", SemanticVersion.Default);
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.AddConsumeDecisionObserver(route, typeof(IConsumeDecisionObserver), (ObserverFailureBehavior)99));
    }
    private readonly PigeonRouteKey _route = new("orders", new SemanticVersion(1, 0, 0), "billing");

    [Fact]
    public void PublishRoute_RequiresBuilder()
        => Assert.Throws<ArgumentNullException>(() => new PigeonPublishRouteBuilder(null, _route));

    [Fact]
    public void ConsumerRoute_RequiresBuilder()
        => Assert.Throws<ArgumentNullException>(() => new PigeonConsumerRouteBuilder(null, _route));

    [Fact]
    public void PublishRoute_RegistersInterceptorForRoute()
    {
        var builder = CreateBuilder();
        var route = new PigeonPublishRouteBuilder(builder, _route);
        Assert.Same(route, route.AddPublishDecisionInterceptor<IPublishDecisionInterceptor>());
        Assert.Contains(typeof(IPublishDecisionInterceptor), builder.RouteInterceptorRegistry.GetPublishDecisionInterceptors(_route));
    }

    [Fact]
    public void ConsumerRoute_RegistersDecisionInterceptor()
    {
        var builder = CreateBuilder();
        var route = new PigeonConsumerRouteBuilder(builder, _route);
        Assert.Same(route, route.AddConsumeDecisionInterceptor<IConsumeDecisionInterceptor>());
        Assert.Contains(typeof(IConsumeDecisionInterceptor), builder.RouteInterceptorRegistry.GetConsumeDecisionInterceptors(_route));
    }

    [Fact]
    public void ConsumerRoute_RegistersExecutionInterceptor()
    {
        var builder = CreateBuilder();
        var route = new PigeonConsumerRouteBuilder(builder, _route);
        Assert.Same(route, route.AddConsumeExecutionInterceptor<IConsumeExecutionInterceptor>());
        Assert.Contains(typeof(IConsumeExecutionInterceptor), builder.RouteInterceptorRegistry.GetConsumeExecutionInterceptors(_route));
    }

    [Fact]
    public void ConsumerRoute_DefaultObserverUsesFailPolicy()
    {
        var builder = CreateBuilder();
        new PigeonConsumerRouteBuilder(builder, _route).AddConsumeDecisionObserver<IConsumeDecisionObserver>();
        var registration = Assert.Single(builder.RouteInterceptorRegistry.GetConsumeDecisionObserverRegistrations(_route));
        Assert.Equal(ObserverFailureBehavior.Fail, registration.Behavior);
    }

    [Fact]
    public void ConsumerRoute_RejectsInvalidObserverPolicy()
    {
        var route = new PigeonConsumerRouteBuilder(CreateBuilder(), _route);
        Assert.Throws<ArgumentOutOfRangeException>(() => route.AddConsumeDecisionObserver<IConsumeDecisionObserver>((ObserverFailureBehavior)999));
    }

    private static GlobalSettingsBuilder CreateBuilder()
        => new(new ServiceCollection(), new ConfigurationBuilder().Build(), Substitute.For<IConsumingConfigurator>(), new MessagingSettings());
}
