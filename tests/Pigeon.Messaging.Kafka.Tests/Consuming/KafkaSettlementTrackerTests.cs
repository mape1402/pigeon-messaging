using Confluent.Kafka;
using NSubstitute;
using Pigeon.Messaging.Kafka.Consuming;

namespace Pigeon.Messaging.Kafka.Tests.Consuming;

public class KafkaSettlementTrackerTests
{
    [Fact]
    public void Complete_CommitsOnlyThroughContiguousCompletedDeliveries()
    {
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        var tracker = new KafkaSettlementTracker();
        var first = Result(0);
        var second = Result(1);
        var firstDelivery = tracker.Register(first);
        var secondDelivery = tracker.Register(second);

        tracker.Complete(consumer, second, secondDelivery);
        consumer.DidNotReceive().Commit(Arg.Any<IEnumerable<TopicPartitionOffset>>());

        tracker.Complete(consumer, first, firstDelivery);
        consumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset.Value == 2));
    }

    [Fact]
    public void Retry_SeeksAndInvalidatesLaterInFlightDeliveries()
    {
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        var tracker = new KafkaSettlementTracker();
        var first = Result(0);
        var second = Result(1);
        var firstDelivery = tracker.Register(first);
        var staleDelivery = tracker.Register(second);

        tracker.Retry(consumer, first, firstDelivery);
        tracker.Complete(consumer, second, staleDelivery);
        consumer.Received(1).Seek(first.TopicPartitionOffset);
        consumer.DidNotReceive().Commit(Arg.Any<IEnumerable<TopicPartitionOffset>>());

        var retry = tracker.Register(first);
        tracker.Complete(consumer, first, retry);
        consumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset.Value == 1));
    }

    [Fact]
    public void Release_InvalidatesCallbacksFromPreviousAssignment()
    {
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        var tracker = new KafkaSettlementTracker();
        var result = Result(3);
        var old = tracker.Register(result);
        tracker.Release(new[] { result.TopicPartition });
        var current = tracker.Register(result);

        tracker.Complete(consumer, result, old);
        tracker.Retry(consumer, result, old);
        consumer.DidNotReceive().Commit(Arg.Any<IEnumerable<TopicPartitionOffset>>());
        consumer.DidNotReceive().Seek(Arg.Any<TopicPartitionOffset>());
        tracker.Complete(consumer, result, current);
        consumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset.Value == 4));
    }

    [Fact]
    public void Retry_DoesNotPreventEarlierDeliveriesFromCompleting()
    {
        var consumer = Substitute.For<IConsumer<Ignore, string>>();
        var tracker = new KafkaSettlementTracker();
        var first = Result(0);
        var second = Result(1);
        var firstDelivery = tracker.Register(first);
        var secondDelivery = tracker.Register(second);

        tracker.Retry(consumer, second, secondDelivery);
        tracker.Complete(consumer, first, firstDelivery);
        consumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset.Value == 1));
        var retry = tracker.Register(second);
        tracker.Complete(consumer, second, retry);
        consumer.Received(1).Commit(Arg.Is<IEnumerable<TopicPartitionOffset>>(offsets => offsets.Single().Offset.Value == 2));
    }

    private static ConsumeResult<Ignore, string> Result(long offset) => new()
    {
        Topic = "orders",
        Partition = new Partition(0),
        Offset = new Offset(offset),
        Message = new Message<Ignore, string> { Value = "{}" }
    };
}
