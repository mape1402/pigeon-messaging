namespace Pigeon.Messaging.Tests.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Topology;

public class GlobalSettingsEdgeTests
{
    [Fact]
    public void PreProvisionRoutes_RequiresArray()
        => Assert.Throws<ArgumentNullException>(() => CreateBuilder().PreProvisionPublishRoutes(null));

    [Fact]
    public void PreProvisionRoutes_SkipsNullRoutes()
    {
        var builder = CreateBuilder();
        var route = PublishingRoute.ForTopic("orders");
        builder.PreProvisionPublishRoutes(null, route);
        Assert.Same(route, Assert.Single(builder.GlobalSettings.PublishTopologyRoutes));
    }

    [Fact]
    public void ConsumerExecution_RequiresConfiguration()
        => Assert.Throws<ArgumentNullException>(() => CreateBuilder().ConfigureConsumerExecution(null));

    [Fact]
    public void ConsumerExecution_AppliesConfiguration()
    {
        var builder = CreateBuilder();
        builder.ConfigureConsumerExecution(settings => settings.MaxConcurrency = 7);
        Assert.Equal(7, builder.GlobalSettings.ConsumerExecution.MaxConcurrency);
    }

    [Fact]
    public void Publishing_RequiresConfiguration()
        => Assert.Throws<ArgumentNullException>(() => CreateBuilder().ConfigurePublishing(null));

    [Fact]
    public void Outbox_RequiresConfiguration()
    {
#pragma warning disable CS0618
        Assert.Throws<ArgumentNullException>(() => CreateBuilder().ConfigureOutbox(null));
#pragma warning restore CS0618
    }

    [Fact]
    public void Outbox_AppliesConfiguration()
    {
        var builder = CreateBuilder();
#pragma warning disable CS0618
        var configured = false;
        builder.ConfigureOutbox(_ => configured = true);
#pragma warning restore CS0618
        Assert.True(configured);
    }

    [Fact]
    public void HighThroughput_ClampsMultipliersAndUsesTimeout()
    {
        var builder = CreateBuilder();
        builder.ConfigureHighThroughputConsumers(0, 0, TimeSpan.FromSeconds(5));
        Assert.Equal(Environment.ProcessorCount, builder.GlobalSettings.ConsumerExecution.MaxConcurrency);
        Assert.Equal(Environment.ProcessorCount, builder.GlobalSettings.ConsumerExecution.QueueCapacity);
        Assert.Equal(TimeSpan.FromSeconds(5), builder.GlobalSettings.ConsumerExecution.HandlerTimeout);
    }

    [Fact]
    public void TopologyMode_IsConfigurable()
    {
        var builder = CreateBuilder();
        builder.SetTopologyProvisioningMode(TopologyProvisioningMode.OnConsume);
        Assert.Equal(TopologyProvisioningMode.OnConsume, builder.GlobalSettings.TopologyProvisioningMode);
    }

    [Fact]
    public void ConsumeHandler_WithSubscriptionUsesExactRoute()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        var builder = CreateBuilder(configurator);
        ConsumeHandler<string> handler = (_, _) => Task.CompletedTask;
        builder.AddConsumeHandler("orders", SemanticVersion.Default, "billing", handler);
        configurator.Received(1).AddConsumer("orders", SemanticVersion.Default, "billing", handler);
    }

    [Fact]
    public void TypedRegistration_RejectsIncompatibleImplementation()
    {
        var builder = CreateBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.AddService(typeof(IDisposable), typeof(string), ServiceLifetime.Transient));
    }

    [Fact]
    public void KeyedRegistration_RejectsIncompatibleImplementation()
        => Assert.Throws<InvalidOperationException>(() => CreateBuilder().AddKeyedService("key", typeof(IDisposable), typeof(string), ServiceLifetime.Transient));

    [Fact]
    public void TypedRegistration_RegistersCompatibleImplementation()
    {
        var builder = CreateBuilder();
        Assert.Same(builder, builder.AddService(typeof(IDisposable), typeof(MemoryStream), ServiceLifetime.Transient));
    }

    [Fact]
    public void KeyedRegistration_RegistersCompatibleImplementation()
    {
        var builder = CreateBuilder();
        Assert.Same(builder, builder.AddKeyedService("key", typeof(IDisposable), typeof(MemoryStream), ServiceLifetime.Transient));
    }

    private static GlobalSettingsBuilder CreateBuilder(IConsumingConfigurator configurator = null)
        => new(new ServiceCollection(), new ConfigurationBuilder().Build(), configurator ?? Substitute.For<IConsumingConfigurator>(), new MessagingSettings());
}
