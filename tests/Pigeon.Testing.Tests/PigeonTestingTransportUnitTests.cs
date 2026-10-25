using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

namespace Pigeon.Testing.Tests;

public class PigeonTestingTransportUnitTests
{
    private readonly IServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private readonly ISerializer _serializer = Substitute.For<ISerializer>();
    private readonly IConsumingConfigurator _configurator = Substitute.For<IConsumingConfigurator>();
    private readonly IConsumingDispatcher _dispatcher = Substitute.For<IConsumingDispatcher>();
    private readonly IRawPayloadFactory _rawFactory = Substitute.For<IRawPayloadFactory>();

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new PigeonTestingTransport(null, _serializer, _configurator, _dispatcher, _rawFactory));
        Assert.Throws<ArgumentNullException>(() => new PigeonTestingTransport(_services, null, _configurator, _dispatcher, _rawFactory));
        Assert.Throws<ArgumentNullException>(() => new PigeonTestingTransport(_services, _serializer, null, _dispatcher, _rawFactory));
        Assert.Throws<ArgumentNullException>(() => new PigeonTestingTransport(_services, _serializer, _configurator, null, _rawFactory));
        Assert.Throws<ArgumentNullException>(() => new PigeonTestingTransport(_services, _serializer, _configurator, _dispatcher, null));
    }

    [Fact]
    public async Task CaptureWrapped_RejectsNullPayload()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Transport().CaptureWrappedAsync<string>(null, PublishingRoute.ForTopic("orders")).AsTask());

    [Fact]
    public async Task CaptureRaw_RejectsNullPayload()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => Transport().CaptureRawAsync<string>(null, PublishingRoute.ForTopic("orders")).AsTask());

    [Fact]
    public void FailNext_RejectsNullException()
        => Assert.Throws<ArgumentNullException>(() => Transport().FailNext<string>(null));

    [Fact]
    public async Task RawMessages_AreCapturedButNotDispatched()
    {
        var transport = Transport();
        await transport.CaptureRawAsync("hello", PublishingRoute.ForTopic("orders"));
        await transport.DispatchPendingAsync();
        Assert.True(Assert.Single(transport.PublishedMessages).IsRaw);
        Assert.Empty(transport.ConsumedMessages);
    }

    [Fact]
    public async Task FailNext_MatchesAssignableMessageType()
    {
        var transport = Transport();
        transport.FailNext<object>(new TimeoutException());
        await transport.CaptureRawAsync("hello", PublishingRoute.ForTopic("orders"));
        await transport.DispatchPendingAsync();
        Assert.IsType<TimeoutException>(Assert.Single(transport.ConsumerFailures).Exception);
    }

    [Fact]
    public async Task DispatchPending_RecordsDispatcherExceptionAsConsumerFailure()
    {
        _configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders", "billing") });
        _dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(),
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("handler failed")));
        var transport = Transport();
        await transport.CaptureWrappedAsync(new WrappedPayload<string> { Message = "hello" }, PublishingRoute.ForTopic("orders"));
        await transport.DispatchPendingAsync();
        var failure = Assert.Single(transport.ConsumerFailures);
        Assert.Equal(PigeonTestingFailureKind.Consumer, failure.Kind);
        Assert.Equal("billing", Assert.Single(transport.DeadLetterMessages).Subscription);
    }

    [Fact]
    public async Task DispatchPending_AllowsDispatcherToSettleDelivery()
    {
        _configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders", "billing") });
        _dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(),
            Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await call.ArgAt<Func<CancellationToken, Task>>(3)(CancellationToken.None);
                await call.ArgAt<Func<Exception, CancellationToken, Task>>(4)(new InvalidOperationException(), CancellationToken.None);
            });
        var transport = Transport();
        await transport.CaptureWrappedAsync(new WrappedPayload<string> { Message = "hello" }, PublishingRoute.ForTopic("orders"));
        await transport.DispatchPendingAsync();
        Assert.Single(transport.ConsumedMessages);
        Assert.Empty(transport.ConsumerFailures);
    }

    [Fact]
    public async Task Clear_RemovesPublishedPendingAndFailures()
    {
        var transport = Transport();
        transport.FailNext<string>(new TimeoutException());
        await transport.CaptureRawAsync("hello", PublishingRoute.ForTopic("orders"));
        await transport.DispatchPendingAsync();
        await transport.CaptureRawAsync("next", PublishingRoute.ForTopic("orders"));
        transport.Clear();
        await transport.DispatchPendingAsync();
        Assert.Empty(transport.Messages);
        Assert.Empty(transport.DeadLetterMessages);
        Assert.Empty(transport.ConsumerFailures);
    }

    [Fact]
    public async Task DispatchPending_ObservesCancellation()
    {
        var transport = Transport();
        await transport.CaptureRawAsync("hello", PublishingRoute.ForTopic("orders"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transport.DispatchPendingAsync(cancellation.Token));
    }

    private PigeonTestingTransport Transport() => new(_services, _serializer, _configurator, _dispatcher, _rawFactory);
}
