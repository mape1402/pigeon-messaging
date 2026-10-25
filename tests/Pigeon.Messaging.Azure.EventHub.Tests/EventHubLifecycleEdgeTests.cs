namespace Pigeon.Messaging.Azure.EventHub.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Topology;
using System.Collections.Concurrent;
using System.Reflection;

public class EventHubLifecycleEdgeTests
{
    [Fact]
    public async Task Start_HandlesTopologyFailure()
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders") });
        var provider = Substitute.For<IEventHubProvider>();
        var topology = Substitute.For<ITopologyProvisioningService>();
        topology.EnsureConsumeTopologyAsync(Arg.Any<ConsumerEndpoint>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException()));
        var adapter = new EventHubConsumingAdapter(configurator, provider, topology, Options.Create(new GlobalSettings()), NullLogger<EventHubConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        provider.DidNotReceive().CreateProcessor(Arg.Any<string>());
        await adapter.StopConsumeAsync();
    }

    [Fact]
    public async Task Stop_ContinuesWhenListenerFaults()
    {
        var adapter = CreateAdapter();
        Field<ConcurrentDictionary<string, Task>>(adapter, "_listeners")["orders::billing"] = Task.FromException(new InvalidOperationException());
        await adapter.StopConsumeAsync();
        Assert.Empty(Field<ConcurrentDictionary<string, Task>>(adapter, "_listeners"));
    }

    [Fact]
    public async Task Stop_ContinuesWhenProcessorDisposeFails()
    {
        var adapter = CreateAdapter();
        var processor = Substitute.For<IEventHubProcessor>();
        processor.DisposeAsync().Returns(ValueTask.FromException(new InvalidOperationException()));
        Field<ConcurrentDictionary<string, IEventHubProcessor>>(adapter, "_processors")["orders::billing"] = processor;
        await adapter.StopConsumeAsync();
        await processor.Received(1).DisposeAsync();
    }

    private static EventHubConsumingAdapter CreateAdapter()
        => new(Substitute.For<IConsumingConfigurator>(), Substitute.For<IEventHubProvider>(),
            Options.Create(new GlobalSettings()), NullLogger<EventHubConsumingAdapter>.Instance);

    private static T Field<T>(EventHubConsumingAdapter adapter, string name)
        => (T)typeof(EventHubConsumingAdapter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(adapter)!;
}
