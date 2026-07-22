using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Consuming;
using Pigeon.Messaging.Consuming.Configuration;

namespace Pigeon.Messaging.Azure.EventHub.Tests.Consuming
{
    public class EventHubSubscriptionConsumingAdapterTests
    {
        [Fact]
        public void StartConsumeAsync_Should_Create_Processor_With_Subscription_As_ConsumerGroup()
        {
            // Arrange
            var topic = "user-created";
            var subscription = "billing";
            var configurator = Substitute.For<IConsumingConfigurator>();
            configurator.GetAllEndpoints().Returns([new ConsumerEndpoint(topic, subscription)]);

            var provider = Substitute.For<IEventHubProvider>();
            var processor = Substitute.For<IEventHubProcessor>();
            provider.CreateProcessor(topic, subscription).Returns(processor);

            var logger = Substitute.For<ILogger<EventHubConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            var adapter = new EventHubConsumingAdapter(configurator, provider, options, logger);

            // Act
            adapter.StartConsumeAsync();

            // Assert
            provider.Received(1).CreateProcessor(topic, subscription);
            provider.DidNotReceive().CreateProcessor(topic);
        }
    }
}
