namespace Pigeon.Messaging.RabbitMq.Tests
{
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Producing;
    using Pigeon.Messaging.Rabbit;
    using RabbitMQ.Client;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class RabbitTopologyAdapterTests
    {
        private readonly IConnectionProvider _connectionProvider = Substitute.For<IConnectionProvider>();
        private readonly IChannel _channel = Substitute.For<IChannel>();

        public RabbitTopologyAdapterTests()
        {
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);
        }

        [Fact]
        public async Task EnsurePublishTopologyAsync_Should_Declare_Queue_When_No_Exchange_Is_Configured()
        {
            var adapter = new RabbitTopologyAdapter(_connectionProvider, Options.Create(new RabbitSettings()));

            await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForTopic("orders.created"));

            await _channel.Received(1).QueueDeclareAsync("orders.created", false, false, false, null, false, Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().ExchangeDeclareAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), null, false, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsurePublishTopologyAsync_Should_Declare_Exchange_For_Routed_Publish()
        {
            var adapter = new RabbitTopologyAdapter(
                _connectionProvider,
                Options.Create(new RabbitSettings { ExchangeType = "topic", DurableExchange = true }));

            await adapter.EnsurePublishTopologyAsync(PublishingRoute.ForExchange("events", "orders.created"));

            await _channel.Received(1).ExchangeDeclareAsync("events", "topic", true, false, null, false, Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().QueueDeclareAsync("orders.created", Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(), null, Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureConsumeTopologyAsync_Should_Declare_Queue_And_Bind_When_Exchange_Is_Configured()
        {
            var adapter = new RabbitTopologyAdapter(
                _connectionProvider,
                Options.Create(new RabbitSettings { Exchange = "events", ExchangeType = "topic", DurableExchange = true }));

            await adapter.EnsureConsumeTopologyAsync(new ConsumerEndpoint("orders.created", "billing"));

            await _channel.Received(1).QueueDeclareAsync("billing", false, false, false, null, false, Arg.Any<CancellationToken>());
            await _channel.Received(1).ExchangeDeclareAsync("events", "topic", true, false, null, false, Arg.Any<CancellationToken>());
            await _channel.Received(1).QueueBindAsync("billing", "events", "orders.created", null, false, Arg.Any<CancellationToken>());
        }
    }
}
