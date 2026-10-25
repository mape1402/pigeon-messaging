namespace Pigeon.Messaging.Azure.ServiceBus.Tests.Consuming;

using global::Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;

public class ServiceBusDeliveryTests
{
    [Fact]
    public async Task Delivery_WithoutSubscriberCompletesWithoutError()
    {
        var (_, processor, receiver) = await CreateAdapterAsync();
        await processor.DeliverAsync(CreateMessage(receiver));
        await receiver.DidNotReceive().CompleteMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_RejectWithoutExceptionUsesDefaultReason()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        adapter.MessageConsumedAsync += async (_, args, _) => await args.RejectAsync();
        await processor.DeliverAsync(CreateMessage(receiver));
        await receiver.Received(1).DeadLetterMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), "Rejected", null, Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Delivery_ForwardsPayloadAndEndpoint()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        MessageConsumedEventArgs consumed = null;
        adapter.MessageConsumedAsync += (_, args, _) => { consumed = args; return ValueTask.CompletedTask; };
        await processor.DeliverAsync(CreateMessage(receiver));
        Assert.Equal("orders", consumed.Topic);
        Assert.Equal("billing", consumed.Subscription);
        Assert.Equal("{\"id\":1}", consumed.RawPayload);
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
        await processor.DeliverAsync(CreateMessage(receiver));
        if (decision == "complete") await receiver.Received(1).CompleteMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<CancellationToken>());
        if (decision == "retry") await receiver.Received(1).AbandonMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<IDictionary<string, object>>(), Arg.Any<CancellationToken>());
        if (decision == "reject") await receiver.Received(1).DeadLetterMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), "InvalidOperationException", "invalid", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_PropagatesHandlerFailure()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        adapter.MessageConsumedAsync += (_, _, _) => ValueTask.FromException(new InvalidOperationException("failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.DeliverAsync(CreateMessage(receiver)));
    }

    [Fact]
    public async Task Delivery_OnReceiveDoesNotSettleThroughReceiver()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync(MessageAcknowledgementMode.OnReceive);
        adapter.MessageConsumedAsync += async (_, args, _) => await args.CompleteAsync();
        await processor.DeliverAsync(CreateMessage(receiver));
        await receiver.DidNotReceive().CompleteMessageAsync(Arg.Any<ServiceBusReceivedMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_InvokesLegacySynchronousSubscriber()
    {
        var (adapter, processor, receiver) = await CreateAdapterAsync();
        var received = new TaskCompletionSource<MessageConsumedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.MessageConsumed += (_, args) => received.TrySetResult(args);
        await processor.DeliverAsync(CreateMessage(receiver));
        Assert.Equal("orders", (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).Topic);
    }

    [Fact]
    public async Task ProcessorError_IsHandled()
    {
        var (_, processor, _) = await CreateAdapterAsync();
        var args = new ProcessErrorEventArgs(new InvalidOperationException(), ServiceBusErrorSource.Receive, "namespace", "entity", CancellationToken.None);
        await processor.ReportErrorAsync(args);
    }

    private static async Task<(ServiceBusConsumingAdapter, TestServiceBusProcessor, ServiceBusReceiver)> CreateAdapterAsync(
        MessageAcknowledgementMode mode = MessageAcknowledgementMode.Manual)
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders", "billing") });
        var processor = new TestServiceBusProcessor();
        var provider = Substitute.For<IServiceBusProvider>();
        provider.CreateProcessor("orders", "billing").Returns(processor);
        var settings = new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { AcknowledgementMode = mode } };
        var adapter = new ServiceBusConsumingAdapter(configurator, provider, Options.Create(settings), NullLogger<ServiceBusConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        return (adapter, processor, Substitute.For<ServiceBusReceiver>());
    }

    private static ProcessMessageEventArgs CreateMessage(ServiceBusReceiver receiver)
        => new(ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{\"id\":1}")), receiver, CancellationToken.None);
}
