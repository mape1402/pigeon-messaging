namespace Pigeon.Messaging.Kafka.Consuming
{
    using Confluent.Kafka;

    internal sealed class KafkaSettlementTracker
    {
        private readonly Dictionary<TopicPartition, SortedDictionary<long, Delivery>> _partitions = new();

        internal sealed class Delivery
        {
            public bool Completed { get; set; }
        }

        // Access is serialized with the consumer's poll, commit, seek and rebalance callbacks.
        public Delivery Register(ConsumeResult<Ignore, string> result)
        {
            if (!_partitions.TryGetValue(result.TopicPartition, out var entries))
                _partitions.Add(result.TopicPartition, entries = new SortedDictionary<long, Delivery>());

            var delivery = new Delivery();
            entries.Add(result.Offset.Value, delivery);
            return delivery;
        }

        public void Complete(IConsumer<Ignore, string> consumer, ConsumeResult<Ignore, string> result, Delivery delivery)
        {
            if (!IsCurrent(result, delivery, out var entries))
                return;

            delivery.Completed = true;
            long? lastCompleted = null;
            foreach (var entry in entries)
            {
                if (!entry.Value.Completed)
                    break;
                lastCompleted = entry.Key;
            }

            if (!lastCompleted.HasValue)
                return;

            consumer.Commit(new[] { new TopicPartitionOffset(result.TopicPartition, new Offset(lastCompleted.Value + 1)) });
            foreach (var offset in entries.Keys.TakeWhile(offset => offset <= lastCompleted.Value).ToArray())
                entries.Remove(offset);
        }

        public void Retry(IConsumer<Ignore, string> consumer, ConsumeResult<Ignore, string> result, Delivery delivery)
        {
            if (!IsCurrent(result, delivery, out var entries))
                return;

            consumer.Seek(result.TopicPartitionOffset);
            foreach (var offset in entries.Keys.Where(offset => offset >= result.Offset.Value).ToArray())
                entries.Remove(offset);
        }

        public void Release(IEnumerable<TopicPartition> partitions)
        {
            foreach (var partition in partitions)
                _partitions.Remove(partition);
        }

        private bool IsCurrent(ConsumeResult<Ignore, string> result, Delivery delivery, out SortedDictionary<long, Delivery> entries)
            => _partitions.TryGetValue(result.TopicPartition, out entries)
               && entries.TryGetValue(result.Offset.Value, out var current)
               && ReferenceEquals(current, delivery);
    }
}
