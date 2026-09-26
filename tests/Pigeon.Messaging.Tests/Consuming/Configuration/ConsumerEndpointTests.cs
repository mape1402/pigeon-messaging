namespace Pigeon.Messaging.Tests.Consuming.Configuration
{
    using Pigeon.Messaging.Consuming.Configuration;

    public class ConsumerEndpointTests
    {
        [Fact]
        public void Constructor_Should_SetTopicAndSubscription()
        {
            var endpoint = new ConsumerEndpoint("orders.created", "billing");

            Assert.Equal("orders.created", endpoint.Topic);
            Assert.Equal("billing", endpoint.Subscription);
            Assert.Equal("orders.created::billing", endpoint.Key);
            Assert.Equal("billing", endpoint.ResourceName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Should_Throw_WhenTopicIsMissing(string topic)
        {
            Assert.Throws<ArgumentException>(() => new ConsumerEndpoint(topic));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Should_UseDefaultSubscription_WhenSubscriptionIsMissing(string subscription)
        {
            var endpoint = new ConsumerEndpoint("orders.created", subscription);

            Assert.Equal(ConsumerEndpoint.DefaultSubscription, endpoint.Subscription);
            Assert.Equal("orders.created", endpoint.ResourceName);
        }
    }
}
