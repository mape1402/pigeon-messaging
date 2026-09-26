namespace Pigeon.Messaging.Tests
{
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Contracts;

    public class PigeonRouteKeyTests
    {
        [Fact]
        public void Constructor_Should_SetRouteValues()
        {
            var key = new PigeonRouteKey("orders.created", new SemanticVersion(2, 1, 0), "billing");

            Assert.Equal("orders.created", key.Topic);
            Assert.Equal(new SemanticVersion(2, 1, 0), key.Version);
            Assert.Equal("billing", key.Subscription);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Should_Throw_WhenTopicIsNullOrWhitespace(string topic)
        {
            Assert.Throws<ArgumentException>(() => new PigeonRouteKey(topic, SemanticVersion.Default));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Should_UseDefaultSubscription_WhenSubscriptionIsMissing(string subscription)
        {
            var key = new PigeonRouteKey("orders.created", SemanticVersion.Default, subscription);

            Assert.Equal(ConsumerEndpoint.DefaultSubscription, key.Subscription);
        }
    }
}
