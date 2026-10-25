namespace Pigeon.Messaging.InMemory.Tests;

using NSubstitute;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Producing;

public class InMemoryTransportUnitTests
{
    [Fact]
    public void Consumer_RequiresBroker()
        => Assert.Throws<ArgumentNullException>(() => new InMemoryConsumingAdapter(null, Substitute.For<IConsumingConfigurator>()));

    [Fact]
    public void Consumer_RequiresConfigurator()
        => Assert.Throws<ArgumentNullException>(() => new InMemoryConsumingAdapter(new InMemoryMessageBroker(), null));

    [Fact]
    public void Registration_RejectsNonPositiveRetryDelay()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ((GlobalSettingsBuilder)null).UseInMemoryBroker(settings => settings.RetryDelay = TimeSpan.Zero));

    [Fact]
    public async Task Publish_RecordsMessageWithoutSubscribers()
    {
        var broker = new InMemoryMessageBroker();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        Assert.Equal("{}", Assert.Single(broker.PublishedMessages).Payload);
    }

    [Fact]
    public async Task Clear_RemovesMessagesAndDeliveries()
    {
        var broker = new InMemoryMessageBroker();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        broker.CreateDelivery(Assert.Single(broker.PublishedMessages), new ConsumerEndpoint("orders"));
        broker.Clear();
        Assert.Empty(broker.PublishedMessages);
        Assert.Empty(broker.Deliveries);
    }

    [Fact]
    public async Task Consumer_StartAndStopAreIdempotent()
    {
        var (broker, adapter) = CreateConsumer();
        await adapter.StopConsumeAsync();
        await adapter.StartConsumeAsync();
        await adapter.StartConsumeAsync();
        await adapter.StopConsumeAsync();
        await adapter.StopConsumeAsync();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        Assert.Empty(broker.Deliveries);
    }

    [Fact]
    public async Task Consumer_RejectMarksDeliveryFailed()
    {
        var (broker, adapter) = CreateConsumer();
        adapter.MessageConsumedAsync += async (_, message, _) => await message.RejectAsync(new InvalidOperationException("invalid"));
        await adapter.StartConsumeAsync();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        var delivery = Assert.Single(broker.Deliveries);
        Assert.True(delivery.Failed);
        Assert.Equal("invalid", delivery.Error);
        await adapter.StopConsumeAsync();
    }

    [Fact]
    public async Task Consumer_RetryAfterStopFailsExplicitly()
    {
        var (broker, adapter) = CreateConsumer();
        MessageConsumedEventArgs delivery = null;
        adapter.MessageConsumedAsync += (_, message, _) => { delivery = message; return ValueTask.CompletedTask; };
        await adapter.StartConsumeAsync();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        await adapter.StopConsumeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => delivery.RetryAsync());
    }

    [Fact]
    public async Task Consumer_SynchronousHandlerReceivesDelivery()
    {
        var (broker, adapter) = CreateConsumer();
        var received = new TaskCompletionSource<MessageConsumedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.MessageConsumed += (_, message) => received.TrySetResult(message);
        await adapter.StartConsumeAsync();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        Assert.Equal("billing", (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).Subscription);
        await adapter.StopConsumeAsync();
    }

    [Fact]
    public async Task Consumer_StopCancelsPendingRetry()
    {
        var (broker, adapter) = CreateConsumer(TimeSpan.FromMinutes(1));
        adapter.MessageConsumedAsync += async (_, message, _) => await message.RetryAsync();
        await adapter.StartConsumeAsync();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        await adapter.StopConsumeAsync();
        Assert.Single(broker.Deliveries);
    }

    [Fact]
    public async Task Consumer_HandlesFailureDuringRedelivery()
    {
        var broker = new InMemoryMessageBroker();
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders") });
        var adapter = new InMemoryConsumingAdapter(broker, configurator, new InMemoryBrokerSettings { RetryDelay = TimeSpan.FromMilliseconds(1) });
        var retried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        adapter.MessageConsumedAsync += async (_, message, _) =>
        {
            if (Interlocked.Increment(ref attempts) == 1) await message.RetryAsync();
            else { retried.TrySetResult(); throw new InvalidOperationException("redelivery failed"); }
        };
        await adapter.StartConsumeAsync();
        await broker.PublishAsync("{}", PublishingRoute.ForTopic("orders"), false);
        await retried.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await adapter.StopConsumeAsync();
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task TopologyAdapter_IsNoop()
    {
        var adapter = new InMemoryTopologyAdapter();
        Assert.Equal("InMemory", adapter.BrokerName);
        await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));
        await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
    }

    private static (InMemoryMessageBroker, InMemoryConsumingAdapter) CreateConsumer(TimeSpan? retryDelay = null)
    {
        var broker = new InMemoryMessageBroker();
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders", "billing") });
        return (broker, new InMemoryConsumingAdapter(broker, configurator,
            retryDelay.HasValue ? new InMemoryBrokerSettings { RetryDelay = retryDelay.Value } : null));
    }
}
