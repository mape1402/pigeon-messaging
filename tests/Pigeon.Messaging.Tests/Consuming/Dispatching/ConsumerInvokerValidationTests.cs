namespace Pigeon.Messaging.Tests.Consuming.Dispatching;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;

public class ConsumerInvokerValidationTests
{
    [Theory]
    [InlineData(0, "serviceProvider")]
    [InlineData(1, "envelopeFactory")]
    [InlineData(2, "routeInterceptorRegistry")]
    [InlineData(3, "handlerPipeline")]
    public void Constructor_RequiresEachDependency(int missing, string parameter)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PigeonConsumerInvoker(
            missing == 0 ? null : Substitute.For<IServiceProvider>(),
            missing == 1 ? null : Substitute.For<IPigeonConsumeEnvelopeFactory>(),
            missing == 2 ? null : new PigeonRouteInterceptorRegistry(),
            missing == 3 ? null : Substitute.For<IConsumeHandlerPipeline>()));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public async Task Invoke_RejectsMissingConsumerConfiguration()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        using var provider = new ServiceCollection().AddSingleton(configurator).BuildServiceProvider();
        var factory = CreateFactory();
        var invoker = new PigeonConsumerInvoker(provider, factory, new PigeonRouteInterceptorRegistry(), Substitute.For<IConsumeHandlerPipeline>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(new PigeonConsumeEnvelope()).AsTask());
    }

    [Fact]
    public async Task Invoke_ExecutesGlobalInterceptorBeforeHandler()
    {
        var interceptor = Substitute.For<IConsumeInterceptor>();
        var configurator = Substitute.For<IConsumingConfigurator>();
        var configuration = new ConsumerConfiguration<string>((_, _) => Task.CompletedTask);
        configurator.GetConfiguration("orders", SemanticVersion.Default, "billing").Returns(configuration);
        using var provider = new ServiceCollection().AddSingleton(configurator).AddSingleton(interceptor).BuildServiceProvider();
        var pipeline = Substitute.For<IConsumeHandlerPipeline>();
        var invoker = new PigeonConsumerInvoker(provider, CreateFactory(), new PigeonRouteInterceptorRegistry(), pipeline);
        await invoker.InvokeAsync(new PigeonConsumeEnvelope());
        await interceptor.Received(1).Intercept(Arg.Any<ConsumeContext>(), CancellationToken.None);
        await pipeline.Received(1).InvokeAsync(Arg.Any<ConsumeContext>(), configuration, CancellationToken.None);
    }

    [Fact]
    public async Task Invoke_UsesDefaultConfigurationWhenConfiguratorReturnsNull()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        var configuration = new ConsumerConfiguration<string>((_, _) => Task.CompletedTask);
        configurator.GetConfiguration("orders", SemanticVersion.Default).Returns(configuration);
        using var provider = new ServiceCollection().AddSingleton(configurator).BuildServiceProvider();
        var pipeline = Substitute.For<IConsumeHandlerPipeline>();
        var invoker = new PigeonConsumerInvoker(provider, CreateFactory(), new PigeonRouteInterceptorRegistry(), pipeline);
        await invoker.InvokeAsync(new PigeonConsumeEnvelope());
        await pipeline.Received(1).InvokeAsync(Arg.Any<ConsumeContext>(), configuration, CancellationToken.None);
    }

    private static IPigeonConsumeEnvelopeFactory CreateFactory()
    {
        var factory = Substitute.For<IPigeonConsumeEnvelopeFactory>();
        factory.CreateContext(Arg.Any<PigeonConsumeEnvelope>(), Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
            .Returns(call => new ConsumeContext { Services = call.ArgAt<IServiceProvider>(1), Topic = "orders", MessageVersion = SemanticVersion.Default, Subscription = "billing" });
        return factory;
    }
}
