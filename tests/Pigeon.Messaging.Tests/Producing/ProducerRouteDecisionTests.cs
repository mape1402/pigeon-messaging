namespace Pigeon.Messaging.Tests.Producing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;

public class ProducerRouteDecisionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RouteDecision_SkipPreventsPublishing(bool raw)
    {
        var interceptor = Substitute.For<IPublishDecisionInterceptor>();
        interceptor.InterceptAsync(Arg.Any<PublishContext>(), Arg.Any<CancellationToken>())
            .Returns(new PigeonPublishDecisionResult(PigeonPublishDecision.Skip));
        using var services = new ServiceCollection().AddSingleton(interceptor).BuildServiceProvider();
        var registry = new PigeonRouteInterceptorRegistry();
        registry.AddPublishDecisionInterceptor(new PigeonRouteKey("orders", SemanticVersion.Default), typeof(IPublishDecisionInterceptor));
        var manager = Substitute.For<IProducingManager>();
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), manager, Options.Create(new GlobalSettings()),
            routeInterceptorRegistry: registry, serviceProvider: services);
        if (raw) await producer.PublishRawAsync("hello", "orders");
        else await producer.PublishAsync("hello", "orders");
        await interceptor.Received(1).InterceptAsync(Arg.Any<PublishContext>(), Arg.Any<CancellationToken>());
        await manager.DidNotReceive().PushRawAsync(Arg.Any<string>(), Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>());
        await manager.DidNotReceive().PushAsync(Arg.Any<WrappedPayload<string>>(), Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteDecision_WithoutServiceProviderContinues()
    {
        var registry = new PigeonRouteInterceptorRegistry();
        registry.AddPublishDecisionInterceptor(new PigeonRouteKey("orders", SemanticVersion.Default), typeof(IPublishDecisionInterceptor));
        var manager = Substitute.For<IProducingManager>();
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), manager, Options.Create(new GlobalSettings()), routeInterceptorRegistry: registry);
        await producer.PublishRawAsync("hello", "orders");
        await manager.Received(1).PushRawAsync("hello", Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxEnabledWithoutStorageThrows()
    {
        var settings = Options.Create(new GlobalSettings { Outbox = new OutboxSettings { Enabled = true } });
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), Substitute.For<IProducingManager>(), settings);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await producer.PublishRawAsync("hello", "orders"));
    }

    [Fact]
    public async Task LegacyOutbox_NotifiesAfterStorageSave()
    {
        var settings = Options.Create(new GlobalSettings { Outbox = new OutboxSettings { Enabled = true } });
        var storage = Substitute.For<IOutboxStorage>();
        var notifier = Substitute.For<IOutboxCommitNotifier>();
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), Substitute.For<IProducingManager>(), settings,
            storage, new OutboxMessageFactory(serializer), notifier);
        await producer.PublishRawAsync("hello", "orders");
        await storage.Received(1).AddAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await storage.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await notifier.Received(1).NotifySavedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RawDecision_UseOutboxPersistsEvenWhenOutboxDisabled()
    {
        var interceptor = Substitute.For<IPublishDecisionInterceptor>();
        interceptor.InterceptAsync(Arg.Any<PublishContext>(), Arg.Any<CancellationToken>()).Returns(new PigeonPublishDecisionResult(PigeonPublishDecision.UseOutbox));
        var storage = Substitute.For<IOutboxStorage>();
        var serializer = Substitute.For<ISerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        var manager = Substitute.For<IProducingManager>();
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), manager, Options.Create(new GlobalSettings()),
            storage, new OutboxMessageFactory(serializer), decisionInterceptors: new[] { interceptor });
        await producer.PublishRawAsync("hello", "orders");
        await storage.Received(1).AddAsync(Arg.Is<OutboxMessage>(message => message.IsRaw), Arg.Any<CancellationToken>());
        await manager.DidNotReceive().PushRawAsync(Arg.Any<string>(), Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RawDecision_PublishNowBypassesEnabledOutbox()
    {
        var interceptor = Substitute.For<IPublishDecisionInterceptor>();
        interceptor.InterceptAsync(Arg.Any<PublishContext>(), Arg.Any<CancellationToken>()).Returns(new PigeonPublishDecisionResult(PigeonPublishDecision.PublishNow));
        var manager = Substitute.For<IProducingManager>();
        var settings = Options.Create(new GlobalSettings { Outbox = new OutboxSettings { Enabled = true } });
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), manager, settings, decisionInterceptors: new[] { interceptor });
        await producer.PublishRawAsync("hello", "orders");
        await manager.Received(1).PushRawAsync("hello", Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishExchange_RequiresMessage(bool raw)
    {
        var producer = new Producer(Array.Empty<IPublishInterceptor>(), Substitute.For<IProducingManager>(), Options.Create(new GlobalSettings()));
        if (raw) await Assert.ThrowsAsync<ArgumentNullException>(async () => await producer.PublishRawAsync<string>(null, "events", "orders"));
        else await Assert.ThrowsAsync<ArgumentNullException>(async () => await producer.PublishAsync<string>(null, "events", "orders", SemanticVersion.Default));
    }
}
