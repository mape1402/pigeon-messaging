namespace Pigeon.Messaging.Kafka.Tests.Consuming;

using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using Pigeon.Messaging.Kafka.Consuming;

public class KafkaDeliveryTests
{
    [Theory]
    [InlineData("complete")]
    [InlineData("reject")]
    public async Task TerminalDelivery_CommitsOffset(string decision)
    {
        var (message, consumer, _) = CreateDelivery();
        if (decision == "complete") await message.CompleteAsync();
        else await message.RejectAsync();
        consumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(x => x.Single().Offset.Value == 4));
    }

    [Theory]
    [InlineData("retry")]
    [InlineData("failure")]
    public async Task FailedDelivery_SeeksToOriginalOffset(string decision)
    {
        var (message, consumer, result) = CreateDelivery();
        if (decision == "retry") await message.RetryAsync();
        else await message.FailAsync(new InvalidOperationException());
        consumer.Received(1).Seek(result.TopicPartitionOffset);
    }

    [Fact]
    public async Task OnReceive_DoesNotCommitManually()
    {
        var (message, consumer, _) = CreateDelivery(MessageAcknowledgementMode.OnReceive);
        await message.CompleteAsync();
        consumer.DidNotReceive().Commit(Arg.Any<IEnumerable<TopicPartitionOffset>>());
    }

    private static (MessageConsumedEventArgs, IConsumer<Ignore, string>, ConsumeResult<Ignore, string>) CreateDelivery(
        MessageAcknowledgementMode mode = MessageAcknowledgementMode.Manual)
    {
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        var result = new ConsumeResult<Ignore, string> { Topic = "orders", Partition = new Partition(0), Offset = new Offset(3), Message = new Message<Ignore, string> { Value = "{}" } };
        var tracker = new KafkaSettlementTracker();
        var settings = Options.Create(new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { AcknowledgementMode = mode } });
        var adapter = new KafkaConsumingAdapter(Substitute.For<IConsumingConfigurator>(), Substitute.For<IConfigurationProvider>(), settings, NullLogger<KafkaConsumingAdapter>.Instance);
        return (adapter.CreateConsumedEvent("orders", "{}", "billing", consumer, result, tracker, tracker.Register(result)), consumer, result);
    }
}
