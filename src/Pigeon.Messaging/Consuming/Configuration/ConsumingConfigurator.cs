namespace Pigeon.Messaging.Consuming.Configuration
{
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System.Collections.Concurrent;

    internal class ConsumingConfigurator : IConsumingConfigurator
    {
        private readonly ConcurrentDictionary<(string, SemanticVersion), ConsumerConfiguration> _consumers = new();

        public IConsumingConfigurator AddConsumer<T>(string topic, SemanticVersion version, ConsumeHandler<T> handler) where T : class
        {
            CheckTopic(topic);

            if(handler == null)
                throw new ArgumentNullException(nameof(handler));

            var consumerConfig = new ConsumerConfiguration<T>(handler)
            {
                Topic = topic,
                Version = version
            };

            if (!_consumers.TryAdd((topic, version), consumerConfig))
                throw new InvalidOperationException($"A consumer for topic '{topic}' with version '{version}' is already registered.");

            return this;
        }

        public IConsumingConfigurator AddConsumer<T>(string topic, ConsumeHandler<T> handler) where T : class
            => AddConsumer(topic, SemanticVersion.Default, handler);

        public IConsumingConfigurator RemoveConsumer(string topic, SemanticVersion version)
        {
            CheckTopic(topic);

            _consumers.TryRemove((topic, version), out _);

            return this;
        }

        public IConsumingConfigurator RemoveConsumer(string topic)
            => RemoveConsumer(topic, SemanticVersion.Default);

        public ConsumerConfiguration GetConfiguration(string topic, SemanticVersion version)
        {
            CheckTopic(topic);

            if(_consumers.TryGetValue((topic, version), out var configuration))
                return configuration;

            throw new InvalidOperationException($"Consumer with Topic '{topic}' and Version '{version}' doesn't exist in current context.");
        }

        public ConsumerConfiguration GetConfiguration(string topic)
            => GetConfiguration(topic, SemanticVersion.Default);

        private void CheckTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));
        }
    }
}
