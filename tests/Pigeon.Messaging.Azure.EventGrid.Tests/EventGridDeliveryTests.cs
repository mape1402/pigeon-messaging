namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;

public class EventGridDeliveryTests
{
    [Fact]
    public async Task Delivery_ExtractsEventDataAndSubscription()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        MessageConsumedEventArgs consumed = null;
        adapter.MessageConsumedAsync += (_, args, _) => { consumed = args; return ValueTask.CompletedTask; };
        await processor.DeliverAsync(Message(receiver));
        Assert.Equal("orders", consumed.Topic);
        Assert.Equal("billing", consumed.Subscription);
        Assert.Equal("hello", consumed.RawPayload);
    }

    [Fact]
    public async Task Delivery_AcceptsCompleteEventGridEnvelope()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        MessageConsumedEventArgs consumed = null;
        adapter.MessageConsumedAsync += (_, args, _) => { consumed = args; return ValueTask.CompletedTask; };
        var json = "{\"id\":\"1\",\"subject\":\"orders\",\"data\":\"hello\",\"eventType\":\"created\",\"dataVersion\":\"1\",\"metadataVersion\":\"1\",\"eventTime\":\"2024-01-01T00:00:00Z\",\"topic\":\"orders\"}";
        var args = new ProcessMessageEventArgs(ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json)), receiver, CancellationToken.None);
        await processor.DeliverAsync(args);
        Assert.Equal("hello", consumed.RawPayload);
    }

    [Fact]
    public async Task Delivery_WithoutSubscriberCompletesWithoutError()
    {
        var (_, processor, receiver) = await CreateAdapterAsync();
        await processor.DeliverAsync(Message(receiver));
        await receiver.DidNotReceive().CompleteMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_RejectWithoutExceptionUsesDefaultReason()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        adapter.MessageConsumedAsync += async (_, args, _) => await args.RejectAsync();
        await processor.DeliverAsync(Message(receiver));
        await receiver.Received(1).DeadLetterMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), "Rejected", null, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("retry")]
    [InlineData("reject")]
    public async Task Delivery_SettlesThroughReceiver(string decision)
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        adapter.MessageConsumedAsync += async (_, args, _) =>
        {
            if (decision == "complete") await args.CompleteAsync();
            if (decision == "retry") await args.RetryAsync();
            if (decision == "reject") await args.RejectAsync(new InvalidOperationException("invalid"));
        };
        await processor.DeliverAsync(Message(receiver));
        if (decision == "complete") await receiver.Received(1).CompleteMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<CancellationToken>());
        if (decision == "retry") await receiver.Received(1).AbandonMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<IDictionary<string, object>>(), Arg.Any<CancellationToken>());
        if (decision == "reject") await receiver.Received(1).DeadLetterMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), "InvalidOperationException", "invalid", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_OnReceiveDoesNotSettleThroughReceiver()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync(MessageAcknowledgementMode.OnReceive);
        adapter.MessageConsumedAsync += async (_, args, _) => await args.CompleteAsync();
        await processor.DeliverAsync(Message(receiver));
        await receiver.DidNotReceive().CompleteMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_PropagatesHandlerFailure()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        adapter.MessageConsumedAsync += (_, _, _) => ValueTask.FromException(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.DeliverAsync(Message(receiver)));
    }

    [Fact]
    public async Task Delivery_InvokesLegacySynchronousSubscriber()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        var received = new TaskCompletionSource<MessageConsumedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.MessageConsumed += (_, args) => received.TrySetResult(args);
        await processor.DeliverAsync(Message(receiver));
        Assert.Equal("hello", (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).RawPayload);
    }

    [Fact]
    public async Task ProcessorError_IsHandled()
    {
        var (_, processor, _) = await CreateAdapterAsync();
        var args = new ProcessErrorEventArgs(new InvalidOperationException(), ServiceBusErrorSource.Receive, "namespace", "entity", CancellationToken.None);
        await processor.ReportErrorAsync(args);
    }

    private static async Task<(EventGridConsumingAdapter, TestServiceBusProcessor, ServiceBusReceiver)> CreateAdapterAsync(
        MessageAcknowledgementMode mode = MessageAcknowledgementMode.Manual)
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders", "billing") });
        var processor = new TestServiceBusProcessor();
        var provider = Substitute.For<IEventGridProvider>();
        provider.CreateProcessor("orders", "billing").Returns(processor);
        var settings = new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { AcknowledgementMode = mode } };
        var adapter = new EventGridConsumingAdapter(configurator, provider, Options.Create(settings), NullLogger<EventGridConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        return (adapter, processor, Substitute.For<ServiceBusReceiver>());
    }

    private static ProcessMessageEventArgs Message(ServiceBusReceiver receiver)
        => new(ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{\"data\":\"hello\"}")), receiver, CancellationToken.None);
}
