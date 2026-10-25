namespace Pigeon.Messaging.Tests.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using System.Text.Json;

public class PigeonServiceExtensionEdgeTests
{
    [Fact]
    public void GlobalExecutionInterceptor_IsRegistered()
    {
        var (builder, services, _) = CreateBuilder();
        builder.AddConsumeExecutionInterceptor<IConsumeExecutionInterceptor>();
        Assert.Contains(services, item => item.ServiceType == typeof(IConsumeExecutionInterceptor));
    }

    [Fact]
    public void GlobalPublishDecisionInterceptor_IsRegistered()
    {
        var (builder, services, _) = CreateBuilder();
        builder.AddPublishDecisionInterceptor<IPublishDecisionInterceptor>();
        Assert.Contains(services, item => item.ServiceType == typeof(IPublishDecisionInterceptor));
    }

    [Fact]
    public void PublishRoute_RegistersByValues()
    {
        var (builder, _, _) = CreateBuilder();
        builder.ForPublish("orders", SemanticVersion.Default, "billing").AddPublishDecisionInterceptor<IPublishDecisionInterceptor>();
        var key = new PigeonRouteKey("orders", SemanticVersion.Default, "billing");
        Assert.Contains(typeof(IPublishDecisionInterceptor), builder.GlobalSettingsBuilder.RouteInterceptorRegistry.GetPublishDecisionInterceptors(key));
    }

    [Fact]
    public void ConsumerRoute_RegistersByValues()
    {
        var (builder, _, _) = CreateBuilder();
        builder.ForConsumer("orders", SemanticVersion.Default, "billing").AddConsumeDecisionInterceptor<IConsumeDecisionInterceptor>();
        var key = new PigeonRouteKey("orders", SemanticVersion.Default, "billing");
        Assert.Contains(typeof(IConsumeDecisionInterceptor), builder.GlobalSettingsBuilder.RouteInterceptorRegistry.GetConsumeDecisionInterceptors(key));
    }

    [Fact]
    public void ConsumeHandler_RouteKeyIsForwarded()
    {
        var (builder, _, configurator) = CreateBuilder();
        var key = new PigeonRouteKey("orders", SemanticVersion.Default, "billing");
        ConsumeHandler<string> handler = (_, _) => Task.CompletedTask;
        builder.AddConsumeHandler(key, handler);
        configurator.Received(1).AddConsumer("orders", SemanticVersion.Default, "billing", handler);
    }

    [Fact]
    public void ConsumeHandler_VersionIsForwarded()
    {
        var (builder, _, configurator) = CreateBuilder();
        ConsumeHandler<string> handler = (_, _) => Task.CompletedTask;
        Assert.Same(builder, builder.AddConsumeHandler("orders", SemanticVersion.Default, handler));
        configurator.Received(1).AddConsumer("orders", SemanticVersion.Default, handler);
    }

    [Fact]
    public void ConsumeHandler_DefaultVersionIsForwarded()
    {
        var (builder, _, configurator) = CreateBuilder();
        ConsumeHandler<string> handler = (_, _) => Task.CompletedTask;
        Assert.Same(builder, builder.AddConsumeHandler("orders", handler));
        configurator.Received(1).AddConsumer("orders", handler);
    }

    [Fact]
    public void CustomSerializer_IsRegisteredAsSingleton()
    {
        var (builder, services, _) = CreateBuilder();
        Assert.Same(builder, builder.AddJsonSerializer<DefaultSerializer>());
        Assert.Contains(services, item => item.ServiceType == typeof(ISerializer) && item.ImplementationType == typeof(DefaultSerializer) && item.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void ConfigureJsonOptions_RequiresCallback()
    {
        var (builder, _, _) = CreateBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.ConfigureJsonOptions(null));
    }

    [Fact]
    public void ConfigureJsonOptions_ChangesOptions()
    {
        var (builder, _, _) = CreateBuilder();
        builder.ConfigureJsonOptions(options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
        Assert.Same(JsonNamingPolicy.CamelCase, builder.GlobalSettingsBuilder.JsonSerializerOptions.PropertyNamingPolicy);
    }

    private static (IPigeonServiceBuilder, IServiceCollection, IConsumingConfigurator) CreateBuilder()
    {
        var services = new ServiceCollection();
        var configurator = Substitute.For<IConsumingConfigurator>();
        var settings = new GlobalSettingsBuilder(services, new ConfigurationBuilder().Build(), configurator, new MessagingSettings());
        return (new PigeonServiceBuilder(settings), services, configurator);
    }
}
