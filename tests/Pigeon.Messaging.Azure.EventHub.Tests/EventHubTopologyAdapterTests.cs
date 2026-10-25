namespace Pigeon.Messaging.Azure.EventHub.Tests
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;

    public class EventHubTopologyAdapterTests
    {
        private readonly EventHubTopologyAdapter _adapter = new();

        [Fact]
        public void BrokerName_IdentifiesEventHub() => Assert.Equal("AzureEventHub", _adapter.BrokerName);

        [Fact]
        public async Task EnsurePublishTopology_CompletesWithoutProvisioning()
            => await _adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders"));

        [Fact]
        public async Task EnsureConsumeTopology_CompletesWithoutProvisioning()
            => await _adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders"));
    }
}
