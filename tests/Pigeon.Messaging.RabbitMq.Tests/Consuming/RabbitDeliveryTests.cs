namespace Pigeon.Messaging.RabbitMq.Tests.Consuming;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Rabbit;
using Pigeon.Messaging.Rabbit.Consuming;
using RabbitMQ.Client;
using System.Text;
using System.Reflection;

public class RabbitDeliveryTests
{
    [Fact]
    public async Task Delivery_ForwardsBodyAndSubscription()
    {
        var (adapter, _, consumer) = await CreateAdapterAsync();
        MessageConsumedEventArgs result = null;
        adapter.MessageConsumedAsync += (_, args, _) => { result = args; return ValueTask.CompletedTask; };
        await DeliverAsync(consumer);
        Assert.Equal("orders", result.Topic);
        Assert.Equal("billing", result.Subscription);
        Assert.Equal("hello", result.RawPayload);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("retry")]
    [InlineData("reject")]
    public async Task Delivery_SettlesThroughChannel(string decision)
    {
        var (adapter, channel, consumer) = await CreateAdapterAsync();
        adapter.MessageConsumedAsync += async (_, args, _) =>
        {
            if (decision == "complete") await args.CompleteAsync();
            if (decision == "retry") await args.RetryAsync();
            if (decision == "reject") await args.RejectAsync();
        };
        await DeliverAsync(consumer);
        if (decision == "complete") await channel.Received(1).BasicAckAsync(42, false, Arg.Any<CancellationToken>());
        if (decision == "retry") await channel.Received(1).BasicNackAsync(42, false, true, Arg.Any<CancellationToken>());
        if (decision == "reject") await channel.Received(1).BasicNackAsync(42, false, false, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delivery_HandlerFailureUsesConfiguredRequeue(bool requeue)
    {
        var (adapter, channel, consumer) = await CreateAdapterAsync(requeue: requeue);
        adapter.MessageConsumedAsync += (_, _, _) => ValueTask.FromException(new InvalidOperationException());
        await DeliverAsync(consumer);
        await channel.Received(1).BasicNackAsync(42, false, requeue, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_OnReceiveDoesNotAckAgain()
    {
        var (adapter, channel, consumer) = await CreateAdapterAsync(MessageAcknowledgementMode.OnReceive);
        adapter.MessageConsumedAsync += async (_, args, _) => await args.CompleteAsync();
        await DeliverAsync(consumer);
        await channel.DidNotReceive().BasicAckAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_CriticalObserverFailureDoesNotNackAgain()
    {
        var (adapter, channel, consumer) = await CreateAdapterAsync();
        var failure = (ConsumeObserverRetryException)Activator.CreateInstance(typeof(ConsumeObserverRetryException),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { new InvalidOperationException("observer"), new InvalidOperationException("retry") }, null)!;
        adapter.MessageConsumedAsync += (_, _, _) => ValueTask.FromException(failure);
        await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => DeliverAsync(consumer));
        await channel.DidNotReceive().BasicNackAsync(Arg.Any<ulong>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delivery_InvokesLegacySynchronousSubscriber()
    {
        var (adapter, _, consumer) = await CreateAdapterAsync();
        var received = new TaskCompletionSource<MessageConsumedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.MessageConsumed += (_, args) => received.TrySetResult(args);
        await DeliverAsync(consumer);
        Assert.Equal("hello", (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).RawPayload);
    }

    private static async Task<(RabbitConsumingAdapter, IChannel, IAsyncBasicConsumer)> CreateAdapterAsync(
        MessageAcknowledgementMode mode = MessageAcknowledgementMode.Manual, bool requeue = false)
    {
        var configurator = Substitute.For<IConsumingConfigurator>();
        configurator.GetAllEndpoints().Returns(new[] { new ConsumerEndpoint("orders", "billing") });
        var channel = Substitute.For<IChannel>();
        var connection = Substitute.For<IConnectionProvider>();
        connection.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(channel);
        var settings = Options.Create(new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { AcknowledgementMode = mode } });
        var adapter = new RabbitConsumingAdapter(connection, configurator, settings,
            Options.Create(new RabbitSettings { RequeueOnFailure = requeue }), NullLogger<RabbitConsumingAdapter>.Instance);
        await adapter.StartConsumeAsync();
        var consumer = channel.ReceivedCalls().SelectMany(call => call.GetArguments()).OfType<IAsyncBasicConsumer>().Single();
        return (adapter, channel, consumer);
    }

    private static Task DeliverAsync(IAsyncBasicConsumer consumer)
        => consumer.HandleBasicDeliverAsync("consumer", 42, false, "exchange", "orders",
            new BasicProperties(), Encoding.UTF8.GetBytes("hello"), CancellationToken.None);
}
