namespace Pigeon.Messaging.Tests.Topology
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Topology;

    public class NoopTopologyProvisioningServiceTests
    {
        [Fact]
        public async Task EnsureTopologyMethods_Should_Complete()
        {
            var service = NoopTopologyProvisioningService.Instance;

            await service.EnsureStartupTopologyAsync();
            await service.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders.created"));
            await service.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders.created", "billing"));

            Assert.Same(service, NoopTopologyProvisioningService.Instance);
        }
    }
}
