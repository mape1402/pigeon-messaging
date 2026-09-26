namespace Pigeon.Messaging.Tests.Producing
{
    using Pigeon.Messaging.Producing;

    public class PublishingRouteTests
    {
        [Fact]
        public void ForTopic_Should_CreateTopicRoute()
        {
            var route = PublishingRoute.ForTopic("orders.created");

            Assert.Equal("orders.created", route.Topic);
            Assert.Equal(string.Empty, route.Exchange);
            Assert.Equal("orders.created", route.RoutingKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ForTopic_Should_Throw_WhenTopicIsNullOrWhitespace(string topic)
        {
            Assert.Throws<ArgumentException>(() => PublishingRoute.ForTopic(topic));
        }

        [Fact]
        public void ForExchange_Should_CreateExchangeRoute()
        {
            var route = PublishingRoute.ForExchange("orders.exchange", "orders.created");

            Assert.Equal("orders.created", route.Topic);
            Assert.Equal("orders.exchange", route.Exchange);
            Assert.Equal("orders.created", route.RoutingKey);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ForExchange_Should_Throw_WhenExchangeIsNullOrWhitespace(string exchange)
        {
            Assert.Throws<ArgumentException>(() => PublishingRoute.ForExchange(exchange, "orders.created"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ForExchange_Should_Throw_WhenRoutingKeyIsNullOrWhitespace(string routingKey)
        {
            Assert.Throws<ArgumentException>(() => PublishingRoute.ForExchange("orders.exchange", routingKey));
        }
    }
}
