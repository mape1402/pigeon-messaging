namespace Pigeon.Messaging.Tests.Producing
{
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Outbox;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Topology;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ProducingManagerTests
    {
        [Fact]
        public async Task PushAsync_Should_Ensure_Topology_Before_Publishing()
        {
            var adapter = Substitute.For<IMessageBrokerProducingAdapter>();
            var topologyProvisioningService = Substitute.For<ITopologyProvisioningService>();
            var manager = new ProducingManager(adapter, topologyProvisioningService);
            var route = PublishingRoute.ForExchange("events", "orders.created");
            var payload = new WrappedPayload<SampleMessage>
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = new SampleMessage(),
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };

            await manager.PushAsync(payload, route);

            Received.InOrder(() =>
            {
                topologyProvisioningService.EnsurePublishTopologyAsync(route, Arg.Any<CancellationToken>());
                adapter.PublishMessageAsync(payload, route, Arg.Any<CancellationToken>());
            });
        }

        [Fact]
        public async Task PushRawAsync_Should_Ensure_Topology_Before_Publishing()
        {
            var adapter = Substitute.For<IMessageBrokerProducingAdapter>();
            var topologyProvisioningService = Substitute.For<ITopologyProvisioningService>();
            var manager = new ProducingManager(adapter, topologyProvisioningService);
            var route = PublishingRoute.ForTopic("orders.created");
            var message = new SampleMessage();

            await manager.PushRawAsync(message, route);

            Received.InOrder(() =>
            {
                topologyProvisioningService.EnsurePublishTopologyAsync(route, Arg.Any<CancellationToken>());
                adapter.PublishRawMessageAsync(message, route, Arg.Any<CancellationToken>());
            });
        }

        [Fact]
        public async Task PushOutboxAsync_Should_Publish_Prepared_Wrapped_Message()
        {
            var adapter = Substitute.For<IMessageBrokerProducingAdapter>();
            var topologyProvisioningService = Substitute.For<ITopologyProvisioningService>();
            var serializer = new TestSerializer();
            var manager = new ProducingManager(adapter, topologyProvisioningService, serializer);
            var route = PublishingRoute.ForExchange("events", "orders.created");
            var payload = new WrappedPayload<SampleMessage>
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = new SampleMessage(),
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };
            var outboxMessage = new OutboxMessage
            {
                Payload = serializer.Serialize(payload),
                PayloadType = payload.GetType().AssemblyQualifiedName,
                Exchange = route.Exchange,
                RoutingKey = route.RoutingKey,
                Topic = route.Topic
            };

            await manager.PushOutboxAsync(outboxMessage);

            await topologyProvisioningService.Received(1).EnsurePublishTopologyAsync(
                Arg.Is<PublishingRoute>(x => x.Exchange == "events" && x.RoutingKey == "orders.created"),
                Arg.Any<CancellationToken>());
            await adapter.Received(1).PublishMessageAsync(
                Arg.Is<WrappedPayload<SampleMessage>>(x => x.Domain == "domain"),
                Arg.Is<PublishingRoute>(x => x.Exchange == "events" && x.RoutingKey == "orders.created"),
                Arg.Any<CancellationToken>());
        }

        private class SampleMessage
        {
        }

        private sealed class TestSerializer : ISerializer
        {
            private readonly JsonSerializerOptions _options = new()
            {
                Converters = { new SemanticVersionJsonConverter() }
            };

            public string Serialize(object payload)
                => JsonSerializer.Serialize(payload, _options);

            public object Deserialize(string rawJson, Type targetType)
                => JsonSerializer.Deserialize(rawJson, targetType, _options);
        }
    }
}
