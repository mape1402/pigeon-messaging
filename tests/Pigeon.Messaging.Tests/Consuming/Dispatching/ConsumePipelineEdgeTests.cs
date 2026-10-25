namespace Pigeon.Messaging.Tests.Consuming.Dispatching;

using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;

public class ConsumePipelineEdgeTests
{
    private readonly PigeonRouteKey _route = new("orders", SemanticVersion.Default);

    [Fact]
    public void HandlerPipeline_RequiresRegistry()
        => Assert.Throws<ArgumentNullException>(() => new ConsumeHandlerPipeline(null));

    [Fact]
    public async Task HandlerPipeline_RequiresContext()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new ConsumeHandlerPipeline(new PigeonRouteInterceptorRegistry()).InvokeAsync(null, Configuration()).AsTask());

    [Fact]
    public async Task HandlerPipeline_RequiresConfiguration()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new ConsumeHandlerPipeline(new PigeonRouteInterceptorRegistry()).InvokeAsync(new ConsumeContext(), null).AsTask());

    [Fact]
    public async Task RouteDecision_ReturnsTerminalResult()
    {
        var interceptor = Substitute.For<IConsumeDecisionInterceptor>();
        var result = new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip);
        interceptor.InterceptAsync(Arg.Any<ConsumeContext>(), Arg.Any<CancellationToken>()).Returns(result);
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IEnumerable<IConsumeDecisionInterceptor>)).Returns(Array.Empty<IConsumeDecisionInterceptor>());
        services.GetService(typeof(IConsumeDecisionInterceptor)).Returns(interceptor);
        var registry = new PigeonRouteInterceptorRegistry();
        registry.AddConsumeDecisionInterceptor(_route, typeof(IConsumeDecisionInterceptor));
        var context = new ConsumeContext { Services = services, Topic = "orders", MessageVersion = SemanticVersion.Default };
        Assert.Same(result, await ConsumeDecisionPipeline.ResolveAsync(context, _route, registry, CancellationToken.None));
    }

    [Fact]
    public async Task RouteDecision_NullResultContinues()
    {
        var interceptor = Substitute.For<IConsumeDecisionInterceptor>();
        interceptor.InterceptAsync(Arg.Any<ConsumeContext>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<PigeonConsumeDecisionResult>(null));
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IEnumerable<IConsumeDecisionInterceptor>)).Returns(Array.Empty<IConsumeDecisionInterceptor>());
        services.GetService(typeof(IConsumeDecisionInterceptor)).Returns(interceptor);
        var registry = new PigeonRouteInterceptorRegistry();
        registry.AddConsumeDecisionInterceptor(_route, typeof(IConsumeDecisionInterceptor));
        var context = new ConsumeContext { Services = services, Topic = "orders", MessageVersion = SemanticVersion.Default };
        Assert.Equal(PigeonConsumeDecision.Continue, (await ConsumeDecisionPipeline.ResolveAsync(context, _route, registry, CancellationToken.None)).Decision);
        await interceptor.Received(1).InterceptAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task GlobalDecision_NullResultContinues()
    {
        var interceptor = Substitute.For<IConsumeDecisionInterceptor>();
        interceptor.InterceptAsync(Arg.Any<ConsumeContext>(), Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult<PigeonConsumeDecisionResult>(null));
        using var services = new ServiceCollection().AddSingleton(interceptor).BuildServiceProvider();
        var context = new ConsumeContext { Services = services, Topic = "orders", MessageVersion = SemanticVersion.Default };

        var result = await ConsumeDecisionPipeline.ResolveAsync(context, _route, null, CancellationToken.None);

        Assert.Equal(PigeonConsumeDecision.Continue, result.Decision);
        await interceptor.Received(1).InterceptAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task ObserverResolutionFailure_Propagates()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IEnumerable<IConsumeDecisionObserver>)).Returns(_ => throw new InvalidOperationException("resolution failed"));
        var context = new ConsumeContext { Services = services, Topic = "orders", MessageVersion = SemanticVersion.Default };
        await Assert.ThrowsAsync<InvalidOperationException>(() => ConsumeDecisionPipeline.ObserveAsync(context, PigeonConsumeDecisionResult.Continue, _route, null, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task ObserverResolutionCancellation_Propagates()
    {
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IEnumerable<IConsumeDecisionObserver>)).Returns(_ => throw new OperationCanceledException());
        using var source = new CancellationTokenSource();
        source.Cancel();
        var context = new ConsumeContext { Services = services, Topic = "orders", MessageVersion = SemanticVersion.Default };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ConsumeDecisionPipeline.ObserveAsync(context, PigeonConsumeDecisionResult.Continue, _route, null, source.Token).AsTask());
    }

    private static ConsumerConfiguration<string> Configuration() => new((_, _) => Task.CompletedTask);
}
