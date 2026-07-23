namespace Pigeon.Messaging.Tests.Topology
{
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class TopologyProvisioningServiceTests
    {
        [Fact]
        public async Task EnsurePublishTopologyAsync_Should_Not_Call_Adapters_When_Mode_Is_Manual()
        {
            var adapter = Substitute.For<IMessageBrokerTopologyAdapter>();
            adapter.BrokerName.Returns("test");
            var service = CreateService(TopologyProvisioningMode.Manual, adapter);

            await service.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders.created"));

            await adapter.DidNotReceive().EnsurePublishTopologyAsync(Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsurePublishTopologyAsync_Should_Call_Adapter_Once_Per_Route_When_OnPublish_Is_Enabled()
        {
            var adapter = Substitute.For<IMessageBrokerTopologyAdapter>();
            adapter.BrokerName.Returns("test");
            var service = CreateService(TopologyProvisioningMode.OnPublish, adapter);
            var route = PublishingRoute.ForExchange("events", "orders.created");

            await service.EnsurePublishTopologyAsync(route);
            await service.EnsurePublishTopologyAsync(route);

            await adapter.Received(1).EnsurePublishTopologyAsync(route, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureConsumeTopologyAsync_Should_Call_Adapter_Once_Per_Endpoint_When_OnConsume_Is_Enabled()
        {
            var adapter = Substitute.For<IMessageBrokerTopologyAdapter>();
            adapter.BrokerName.Returns("test");
            var service = CreateService(TopologyProvisioningMode.OnConsume, adapter);
            var endpoint = new ConsumerEndpoint("orders.created", "billing");

            await service.EnsureConsumeTopologyAsync(endpoint);
            await service.EnsureConsumeTopologyAsync(endpoint);

            await adapter.Received(1).EnsureConsumeTopologyAsync(endpoint, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureStartupTopologyAsync_Should_Ensure_Configured_Consumers_When_OnStartup_Is_Enabled()
        {
            var adapter = Substitute.For<IMessageBrokerTopologyAdapter>();
            adapter.BrokerName.Returns("test");
            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllEndpoints().Returns(new[]
            {
                new ConsumerEndpoint("orders.created", "billing"),
                new ConsumerEndpoint("orders.created", "notifications")
            });
            var service = CreateService(TopologyProvisioningMode.OnStartup, adapter, configurator);

            await service.EnsureStartupTopologyAsync();

            await adapter.Received(1).EnsureConsumeTopologyAsync(
                Arg.Is<ConsumerEndpoint>(endpoint => endpoint.Topic == "orders.created" && endpoint.Subscription == "billing"),
                Arg.Any<CancellationToken>());
            await adapter.Received(1).EnsureConsumeTopologyAsync(
                Arg.Is<ConsumerEndpoint>(endpoint => endpoint.Topic == "orders.created" && endpoint.Subscription == "notifications"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureTopologyAsync_Should_Retry_When_Previous_Attempt_Failed()
        {
            var adapter = Substitute.For<IMessageBrokerTopologyAdapter>();
            adapter.BrokerName.Returns("test");
            adapter
                .EnsurePublishTopologyAsync(Arg.Any<PublishingRoute>(), Arg.Any<CancellationToken>())
                .Returns(
                    _ => Task.FromException(new InvalidOperationException("boom")),
                    _ => Task.CompletedTask);
            var service = CreateService(TopologyProvisioningMode.OnPublish, adapter);
            var route = PublishingRoute.ForTopic("orders.created");

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsurePublishTopologyAsync(route));
            await service.EnsurePublishTopologyAsync(route);

            await adapter.Received(2).EnsurePublishTopologyAsync(route, Arg.Any<CancellationToken>());
        }

        private static TopologyProvisioningService CreateService(
            TopologyProvisioningMode mode,
            IMessageBrokerTopologyAdapter adapter,
            IConsumingConfigurator consumingConfigurator = null)
        {
            if (consumingConfigurator == null)
            {
                consumingConfigurator = Substitute.For<IConsumingConfigurator>();
                consumingConfigurator.GetAllEndpoints().Returns(Array.Empty<ConsumerEndpoint>());
            }

            return new TopologyProvisioningService(
                new[] { adapter },
                consumingConfigurator,
                Options.Create(new GlobalSettings { TopologyProvisioningMode = mode }));
        }
    }
}
