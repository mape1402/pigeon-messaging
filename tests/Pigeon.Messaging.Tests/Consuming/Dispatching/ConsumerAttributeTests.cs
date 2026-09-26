namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;

    public class ConsumerAttributeTests
    {
        [Fact]
        public void Constructor_Should_UseDefaultVersion()
        {
            var attribute = new ConsumerAttribute("orders.created");

            Assert.Equal("orders.created", attribute.Topic);
            Assert.Equal(SemanticVersion.Default.ToString(), attribute.Version);
            Assert.Null(attribute.Subscription);
        }

        [Fact]
        public void Constructor_Should_SetVersionAndSubscription()
        {
            var attribute = new ConsumerAttribute("orders.created", "2.0.0", "billing");

            Assert.Equal("orders.created", attribute.Topic);
            Assert.Equal("2.0.0", attribute.Version);
            Assert.Equal("billing", attribute.Subscription);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_Should_Throw_WhenTopicIsMissing(string topic)
        {
            Assert.Throws<ArgumentNullException>(() => new ConsumerAttribute(topic, "1.0.0"));
        }

        [Fact]
        public void Constructor_Should_Throw_WhenVersionIsInvalid()
        {
            Assert.Throws<FormatException>(() => new ConsumerAttribute("orders.created", "invalid"));
        }
    }
}
