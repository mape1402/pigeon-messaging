namespace Pigeon.Messaging.Tests.Producing
{
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Producing.Management;
    using Pigeon.Messaging.Topology;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
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

        private class SampleMessage
        {
        }
    }
}
