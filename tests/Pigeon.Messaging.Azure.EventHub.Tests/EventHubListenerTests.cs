namespace Pigeon.Messaging.Azure.EventHub.Tests;

using global::Azure.Messaging.EventHubs;
using global::Azure.Messaging.EventHubs.Consumer;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using System.Reflection;

public class EventHubListenerTests
{
    [Fact]
    public async Task Listener_ContinuesAfterHandlerFailure()
    {
        var adapter = CreateAdapter();
        var deliveries = 0;
        adapter.MessageConsumedAsync += (_, _, _) =>
        {
            deliveries++;
            return deliveries == 1 ? ValueTask.FromException(new InvalidOperationException()) : ValueTask.CompletedTask;
        };
        await ListenAsync(adapter, Events());
        Assert.Equal(2, deliveries);
    }

    [Fact]
    public async Task Listener_InvokesLegacySynchronousSubscriber()
    {
        var adapter = CreateAdapter();
        var received = new TaskCompletionSource<MessageConsumedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.MessageConsumed += (_, args) => received.TrySetResult(args);
        await ListenAsync(adapter, Events());
        Assert.Equal("{}", (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).RawPayload);
    }

    [Fact]
    public async Task Listener_HandlesReadFailure()
        => await ListenAsync(CreateAdapter(), FailedEvents());

    private static EventHubConsumingAdapter CreateAdapter()
        => new(Substitute.For<IConsumingConfigurator>(), Substitute.For<IEventHubProvider>(),
            Options.Create(new GlobalSettings()), NullLogger<EventHubConsumingAdapter>.Instance);

    private static Task ListenAsync(EventHubConsumingAdapter adapter, IAsyncEnumerable<PartitionEvent> events)
    {
        var processor = Substitute.For<IEventHubProcessor>();
        processor.ReadEventsAsync(Arg.Any<CancellationToken>()).Returns(events);
        return (Task)typeof(EventHubConsumingAdapter).GetMethod("ListenToEvents", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(adapter, new object[] { processor, new ConsumerEndpoint("orders", "billing"), CancellationToken.None })!;
    }

    private static async IAsyncEnumerable<PartitionEvent> Events()
    {
        var context = (PartitionContext)Activator.CreateInstance(typeof(PartitionContext), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { "test", "hub", "group", "0" }, null)!;
        yield return new PartitionEvent(context, new EventData(BinaryData.FromString("{}")));
        await Task.Yield();
        yield return new PartitionEvent(context, new EventData(BinaryData.FromString("{}")));
    }

    private static async IAsyncEnumerable<PartitionEvent> FailedEvents()
    {
        await Task.Yield();
        throw new InvalidOperationException("read failure");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
