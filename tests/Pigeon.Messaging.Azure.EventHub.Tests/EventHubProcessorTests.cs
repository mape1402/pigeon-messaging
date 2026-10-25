namespace Pigeon.Messaging.Azure.EventHub.Tests;

using global::Azure.Messaging.EventHubs;
using global::Azure.Messaging.EventHubs.Consumer;
using NSubstitute;
using System.Reflection;

public class EventHubProcessorTests
{
    [Fact]
    public void Constructor_RequiresSettings()
        => Assert.Throws<ArgumentNullException>(() => new EventHubProcessor(null, "hub", "group"));

    [Fact]
    public void Constructor_RequiresHubName()
        => Assert.Throws<ArgumentNullException>(() => new EventHubProcessor(new AzureEventHubSettings(), null, "group"));

    [Fact]
    public void Constructor_RequiresInjectedClient()
        => Assert.Throws<ArgumentNullException>(() => new EventHubProcessor(new AzureEventHubSettings(), "hub", "group", null));

    [Fact]
    public async Task ReadEvents_MergesAllPartitionsAndSkipsEmptyEvents()
    {
        var client = CreateClient("0", "1");
        client.ReadEventsFromPartitionAsync(Arg.Any<string>(), Arg.Any<EventPosition>(), Arg.Any<CancellationToken>())
            .Returns(call => Events(call.ArgAt<string>(0)));
        await using var processor = CreateProcessor(client);

        var results = await CollectAsync(processor.ReadEventsAsync());

        Assert.Equal(new[] { "0", "1" }, results.Select(x => x.Data.EventBody.ToString()).OrderBy(x => x));
    }

    [Fact]
    public async Task ReadEvents_NoPartitionsReturnsNoEvents()
    {
        await using var processor = CreateProcessor(CreateClient());
        Assert.Empty(await CollectAsync(processor.ReadEventsAsync()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadEvents_PartitionFailureEndsThatPartition(bool cancellation)
    {
        var client = CreateClient("0");
        client.ReadEventsFromPartitionAsync(Arg.Any<string>(), Arg.Any<EventPosition>(), Arg.Any<CancellationToken>())
            .Returns(FailingEvents(cancellation));
        await using var processor = CreateProcessor(client);
        Assert.Empty(await CollectAsync(processor.ReadEventsAsync()));
    }

    [Fact]
    public async Task Dispose_ClosesInjectedClient()
    {
        var client = CreateClient();
        await CreateProcessor(client).DisposeAsync();
        await client.Received(1).DisposeAsync();
    }

    private static EventHubProcessor CreateProcessor(EventHubConsumerClient client)
        => new(new AzureEventHubSettings { ConsumerGroup = "group" }, "hub", "group", client);

    private static EventHubConsumerClient CreateClient(params string[] partitions)
    {
        var client = Substitute.For<EventHubConsumerClient>();
        client.GetPartitionIdsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(partitions));
        return client;
    }

    private static async IAsyncEnumerable<PartitionEvent> Events(string body)
    {
        var context = (PartitionContext)Activator.CreateInstance(typeof(PartitionContext), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { "test", "hub", "group", "0" }, null);
        yield return default;
        await Task.Yield();
        yield return new PartitionEvent(context, new EventData(BinaryData.FromString(body)));
    }

    private static async IAsyncEnumerable<PartitionEvent> FailingEvents(bool cancellation)
    {
        await Task.Yield();
        if (cancellation) throw new OperationCanceledException();
        throw new InvalidOperationException("partition failed");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async Task<List<PartitionEvent>> CollectAsync(IAsyncEnumerable<PartitionEvent> events)
    {
        var results = new List<PartitionEvent>();
        await foreach (var item in events) results.Add(item);
        return results;
    }
}
