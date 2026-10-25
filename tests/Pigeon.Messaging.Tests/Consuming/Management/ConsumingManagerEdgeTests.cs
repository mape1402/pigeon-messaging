namespace Pigeon.Messaging.Tests.Consuming.Management;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Topology;
using System.Reflection;

public class ConsumingManagerEdgeTests
{
    private const string Payload = "{\"Domain\":\"test-domain\",\"MessageVersion\":\"1.2.3\",\"CreatedOnUtc\":\"2024-01-01T00:00:00Z\",\"Message\":{\"Text\":\"Hello\"},\"Metadata\":{\"Key\":{\"Prop\":\"Value\"}}}";

    [Theory]
    [InlineData(0, "dispatcher")]
    [InlineData(1, "messageBrokerAdapters")]
    [InlineData(2, "consumingConfigurator")]
    [InlineData(3, "topologyProvisioningService")]
    [InlineData(4, "globalSettings")]
    [InlineData(5, "diagnostics")]
    [InlineData(6, "logger")]
    public void Constructor_RequiresEachDependency(int missing, string parameter)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ConsumingManager(
            missing == 0 ? null : Substitute.For<IConsumingDispatcher>(),
            missing == 1 ? null : Array.Empty<IMessageBrokerConsumingAdapter>(),
            missing == 2 ? null : Substitute.For<IConsumingConfigurator>(),
            missing == 3 ? null : Substitute.For<ITopologyProvisioningService>(),
            missing == 4 ? null : Options.Create(new GlobalSettings()),
            missing == 5 ? null : new ConsumerExecutionDiagnostics(),
            missing == 6 ? null : NullLogger<ConsumingManager>.Instance));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public async Task Stop_ContinuesWhenAdapterFails()
    {
        var adapter = Substitute.For<IMessageBrokerConsumingAdapter>();
        adapter.StopConsumeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromException(new InvalidOperationException()));
        var manager = CreateManager(Substitute.For<IConsumingDispatcher>(), adapter);
        await manager.StartAsync();
        await manager.StopAsync();
        await adapter.Received(1).StopConsumeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_UsesExplicitSubscriptionAndStripsDomain()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        var manager = CreateManager(dispatcher, settings: new GlobalSettings { Domain = "sales" });
        await manager.StartAsync();
        await DeliverAsync(manager, new MessageConsumedEventArgs("sales.orders", Payload, "billing"));
        await dispatcher.Received(1).DispatchAsync("orders", "billing", Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>());
        await manager.StopAsync();
    }

    [Fact]
    public async Task Dispatch_ManualFailureDoesNotSettleAutomatically()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        var manager = CreateManager(dispatcher);
        var failed = false;
        var message = new MessageConsumedEventArgs("orders", Payload, "billing", _ => Task.CompletedTask,
            (_, _) => { failed = true; return Task.CompletedTask; });
        await manager.StartAsync();
        await DeliverAsync(manager, message);
        Assert.False(failed);
        await manager.StopAsync();
    }

    [Fact]
    public async Task Dispatch_PropagatesRequestedCancellation()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>()).Returns(_ => throw new OperationCanceledException());
        var manager = CreateManager(dispatcher);
        await manager.StartAsync();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DeliverAsync(manager, new MessageConsumedEventArgs("orders", Payload), source.Token));
        await manager.StopAsync();
    }

    [Fact]
    public async Task QueueWithoutConcurrencyLimit_DispatchesMessage()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>()).Returns(_ => { dispatched.TrySetResult(); return Task.CompletedTask; });
        var manager = CreateManager(dispatcher, settings: new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { QueueCapacity = 2 } });
        await manager.StartAsync();
        await DeliverAsync(manager, new MessageConsumedEventArgs("orders", Payload));
        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await manager.StopAsync();
    }

    [Fact]
    public async Task Dispatch_PropagatesObserverRetryFailure()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        var failure = new ConsumeObserverRetryException(new InvalidOperationException(), new TimeoutException());
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));
        var manager = CreateManager(dispatcher);
        await manager.StartAsync();
        var thrown = await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => DeliverAsync(manager, new MessageConsumedEventArgs("orders", Payload)));
        Assert.Same(failure, thrown);
        await manager.StopAsync();
    }

    [Fact]
    public async Task QueueClosed_RetriesMessageThatCannotBeEnqueued()
    {
        var manager = CreateManager(Substitute.For<IConsumingDispatcher>(), settings: new GlobalSettings
        {
            ConsumerExecution = new ConsumerExecutionSettings { MaxConcurrency = 1 }
        });
        await manager.StartAsync();
        await manager.StopAsync();
        Exception failure = null;
        var message = new MessageConsumedEventArgs("orders", Payload, "billing", null,
            (error, _) => { failure = error; return Task.CompletedTask; });
        await DeliverAsync(manager, message);
        Assert.IsType<System.Threading.Channels.ChannelClosedException>(failure);
    }

    [Fact]
    public async Task QueuedWorker_ContinuesAfterObserverRetryFailure()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>()).Returns(
                _ => Task.FromException(new ConsumeObserverRetryException(new InvalidOperationException(), new TimeoutException())),
                _ => { dispatched.TrySetResult(); return Task.CompletedTask; });
        var manager = CreateManager(dispatcher, settings: new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { MaxConcurrency = 1 } });
        await manager.StartAsync();
        await DeliverAsync(manager, new MessageConsumedEventArgs("orders", Payload));
        await DeliverAsync(manager, new MessageConsumedEventArgs("orders", Payload));
        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await manager.StopAsync();
    }

    [Fact]
    public async Task LegacyDelivery_UsesConfiguredQueue()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(_ => { dispatched.TrySetResult(); return Task.CompletedTask; });
        var manager = CreateManager(dispatcher, settings: new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { MaxConcurrency = 1 } });
        await manager.StartAsync();
        typeof(ConsumingManager).GetMethod("MessageConsumed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, new object[] { null, new MessageConsumedEventArgs("orders", Payload) });
        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await manager.StopAsync();
    }

    [Fact]
    public async Task DispatcherCompletion_ConfirmsDelivery()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<CancellationToken, Task>>(3)(CancellationToken.None));
        var completed = false;
        var message = new MessageConsumedEventArgs("orders", Payload, "billing", _ => { completed = true; return Task.CompletedTask; }, null);
        var manager = CreateManager(dispatcher);
        await manager.StartAsync();
        await DeliverAsync(manager, message);
        Assert.True(completed);
        await manager.StopAsync();
    }

    [Fact]
    public async Task DispatcherFailure_RequestsRetry()
    {
        var dispatcher = Substitute.For<IConsumingDispatcher>();
        var failure = new InvalidOperationException("failed");
        dispatcher.DispatchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RawPayload>(), Arg.Any<Func<CancellationToken, Task>>(),
            Arg.Any<Func<Exception, CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<Func<Exception, CancellationToken, Task>>(4)(failure, CancellationToken.None));
        Exception received = null;
        var message = new MessageConsumedEventArgs("orders", Payload, "billing", null, (error, _) => { received = error; return Task.CompletedTask; });
        var manager = CreateManager(dispatcher);
        await manager.StartAsync();
        await DeliverAsync(manager, message);
        Assert.Same(failure, received);
        await manager.StopAsync();
    }

    private static ConsumingManager CreateManager(IConsumingDispatcher dispatcher, IMessageBrokerConsumingAdapter adapter = null, GlobalSettings settings = null)
        => new(dispatcher, adapter == null ? Array.Empty<IMessageBrokerConsumingAdapter>() : new[] { adapter },
            Options.Create(settings ?? new GlobalSettings()), NullLogger<ConsumingManager>.Instance);

    private static Task DeliverAsync(ConsumingManager manager, MessageConsumedEventArgs message, CancellationToken cancellationToken = default)
        => ((ValueTask)typeof(ConsumingManager).GetMethod("MessageConsumedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, new object[] { null, message, cancellationToken })!).AsTask();
}
