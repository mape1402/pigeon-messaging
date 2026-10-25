using System.Reflection;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;

namespace Pigeon.Messaging.Azure.EventHub.Tests;

public class EventHubObserverFailureTests
{
    [Fact]
    public async Task CriticalObserverFailure_StopsListenerAndPropagates()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        var provider = Substitute.For<IEventHubProvider>();
        var processor = Substitute.For<IEventHubProcessor>();
        processor.ReadEventsAsync(Arg.Any<CancellationToken>()).Returns(Events());
        var adapter = new EventHubConsumingAdapter(configurator, provider, Options.Create(new GlobalSettings()), NullLogger<EventHubConsumingAdapter>.Instance);
        var deliveries = 0;
        var failure = (ConsumeObserverRetryException)Activator.CreateInstance(
            typeof(ConsumeObserverRetryException), BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { new InvalidOperationException("observer"), new NotSupportedException("retry") }, null);
        adapter.MessageConsumedAsync += (_, _, _) =>
        {
            Interlocked.Increment(ref deliveries);
            return ValueTask.FromException(failure);
        };
        var listener = typeof(EventHubConsumingAdapter).GetMethod("ListenToEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)listener.Invoke(adapter, new object[] { processor, new ConsumerEndpoint("orders"), CancellationToken.None });

        Assert.Same(failure, await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => task));
        Assert.Equal(1, deliveries);
    }

    private static async IAsyncEnumerable<PartitionEvent> Events()
    {
        var context = (PartitionContext)Activator.CreateInstance(typeof(PartitionContext), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { "test", "hub", "group", "0" }, null);
        yield return new PartitionEvent(context, new EventData(BinaryData.FromString("{}")));
        await Task.Yield();
        yield return new PartitionEvent(context, new EventData(BinaryData.FromString("{}")));
    }
}
