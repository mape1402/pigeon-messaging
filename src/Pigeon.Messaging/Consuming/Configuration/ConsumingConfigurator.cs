namespace Pigeon.Messaging.Consuming.Configuration
{
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    internal class ConsumingConfigurator : IConsumingConfigurator
    {
        private readonly ConcurrentDictionary<(string Topic, SemanticVersion Version, string Subscription), ConsumerConfiguration> _consumers = new();

        public event EventHandler<TopicEventArgs> TopicCreated;

        public event EventHandler<TopicEventArgs> TopicRemoved;

        public IConsumingConfigurator AddConsumer<T>(string topic, SemanticVersion version, ConsumeHandler<T> handler) where T : class
            => AddConsumer(topic, version, ConsumerEndpoint.DefaultSubscription, handler);

        public IConsumingConfigurator AddConsumer<T>(string topic, SemanticVersion version, string subscription, ConsumeHandler<T> handler) where T : class
        {
            CheckTopic(topic);
            subscription = NormalizeSubscription(subscription);

            if(handler == null)
                throw new ArgumentNullException(nameof(handler));

            var consumerConfig = new ConsumerConfiguration<T>(handler)
            {
                Topic = topic,
                Version = version,
                Subscription = subscription
            };

            if (!_consumers.TryAdd((topic, version, subscription), consumerConfig))
                throw new InvalidOperationException($"A consumer for topic '{topic}' with version '{version}' and subscription '{subscription}' is already registered.");

            TopicCreated?.Invoke(this, new TopicEventArgs(topic, subscription));

            return this;
        }

        public IConsumingConfigurator AddConsumer<T>(string topic, ConsumeHandler<T> handler) where T : class
            => AddConsumer(topic, SemanticVersion.Default, handler);

        public IConsumingConfigurator RemoveConsumer(string topic, SemanticVersion version)
        {
            CheckTopic(topic);

            _consumers.TryRemove((topic, version, ConsumerEndpoint.DefaultSubscription), out _);

            if(!_consumers.Keys.Any(x => x.Topic == topic))
                TopicRemoved?.Invoke(this, new TopicEventArgs(topic));

            return this;
        }

        public IConsumingConfigurator RemoveConsumer(string topic)
            => RemoveConsumer(topic, SemanticVersion.Default);

        public ConsumerConfiguration GetConfiguration(string topic, SemanticVersion version)
            => GetConfiguration(topic, version, ConsumerEndpoint.DefaultSubscription);

        public ConsumerConfiguration GetConfiguration(string topic, SemanticVersion version, string subscription)
        {
            CheckTopic(topic);
            subscription = NormalizeSubscription(subscription);

            if(_consumers.TryGetValue((topic, version, subscription), out var configuration))
                return configuration;

            throw new InvalidOperationException($"Consumer with Topic '{topic}', Version '{version}', and Subscription '{subscription}' doesn't exist in current context.");
        }

        public ConsumerConfiguration GetConfiguration(string topic)
            => GetConfiguration(topic, SemanticVersion.Default);

        public IEnumerable<string> GetAllTopics()
            => _consumers.Keys.Select(x => x.Topic).Distinct();

        public IEnumerable<ConsumerEndpoint> GetAllEndpoints()
            => _consumers.Keys
                .Select(x => new ConsumerEndpoint(x.Topic, x.Subscription))
                .DistinctBy(x => x.Key);

        private void CheckTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
                throw new ArgumentException("Topic cannot be null or empty.", nameof(topic));
        }

        private static string NormalizeSubscription(string subscription)
            => string.IsNullOrWhiteSpace(subscription) ? ConsumerEndpoint.DefaultSubscription : subscription;
    }
}
