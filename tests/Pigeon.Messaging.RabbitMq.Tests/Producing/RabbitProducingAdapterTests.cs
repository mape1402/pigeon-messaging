namespace Pigeon.Messaging.RabbitMq.Tests.Producing
{
    using Microsoft.Extensions.Logging;
    using NSubstitute;
    using Pigeon.Messaging.Contracts;
    using Pigeon.Messaging.Rabbit;
    using Pigeon.Messaging.Rabbit.Producing;
    using RabbitMQ.Client;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class RabbitProducingAdapterTests
    {
        private readonly IConnectionProvider _connectionProvider;
        private readonly ILogger<RabbitProducingAdapter> _logger;
        private readonly IChannel _channel;

        public RabbitProducingAdapterTests()
        {
            _connectionProvider = Substitute.For<IConnectionProvider>();
            _logger = Substitute.For<ILogger<RabbitProducingAdapter>>();
            _channel = Substitute.For<IChannel>();
        }

        [Fact]
        public async Task Should_Publish_Message_And_Declare_Topic_Once()
        {
            // Arrange
            _channel.IsOpen.Returns(true);
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(_channel));
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var adapter = new RabbitProducingAdapter(_connectionProvider, serializer, _logger);
            var payload = new WrappedPayload<SampleMessage>()
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = SampleMessage.Instance,
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };

            var topic = "test-topic";

            // Act
            await adapter.PublishMessageAsync(payload, topic);
            await adapter.PublishMessageAsync(payload, topic); // should NOT declare again

            // Assert: QueueDeclareAsync called only once per topic
            await _channel.Received(1).QueueDeclareAsync(topic, false, false, false, null, false, Arg.Any<CancellationToken>());

            // Assert: BasicPublishAsync called twice
            await _channel.Received(2).BasicPublishAsync(
                string.Empty,
                topic,
                Arg.Any<bool>(),
                Arg.Any<BasicProperties>(),
                Arg.Any<ReadOnlyMemory<byte>>(),
                Arg.Any<CancellationToken>()
            );
        }

        [Fact]
        public async Task Should_Create_New_Channel_When_None_Exists()
        {
            // Arrange: _channel is null to simulate no channel yet
            _channel.IsOpen.Returns(true);
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var adapter = new RabbitProducingAdapter(_connectionProvider, serializer, _logger);
            var payload = new WrappedPayload<SampleMessage>()
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = SampleMessage.Instance,
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };

            // Act
            await adapter.PublishMessageAsync(payload, "my-queue");

            // Assert: should create channel
            await _connectionProvider.Received(1).CreateChannelAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Create_New_Channel_When_Existing_Channel_IsClosed()
        {
            // Arrange: _channel.IsOpen returns false
            _channel.IsOpen.Returns(false);
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var adapter = new RabbitProducingAdapter(_connectionProvider, serializer, _logger);

            // Force internal _channel
            adapter.GetType().GetField("_channel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(adapter, _channel);
            var payload = new WrappedPayload<SampleMessage>()
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = SampleMessage.Instance,
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };

            // Act
            await adapter.PublishMessageAsync(payload, "queue");

            // Assert: Should recreate channel if closed
            await _connectionProvider.Received(1).CreateChannelAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Log_And_Rethrow_When_Exception_Occurs()
        {
            // Arrange
            _channel.IsOpen.Returns(true);
            _channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
                    .Returns(_ => throw new InvalidOperationException("Publish failed"));
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var adapter = new RabbitProducingAdapter(_connectionProvider, serializer, _logger);
            var payload = new WrappedPayload<SampleMessage>()
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = SampleMessage.Instance,
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await adapter.PublishMessageAsync(payload, "bad-queue"));
        }

        [Fact]
        public async Task Should_Release_Semaphore_When_Exception_Occurs()
        {
            // Arrange: Make channel throw to test semaphore release
            _channel.IsOpen.Returns(true);
            _channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
                    .Returns(_ => throw new Exception("fail"));
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var adapter = new RabbitProducingAdapter(_connectionProvider, serializer, _logger);
            var payload = new WrappedPayload<SampleMessage>()
            {
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Domain = "domain",
                Message = SampleMessage.Instance,
                MessageVersion = SemanticVersion.Default,
                Metadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>())
            };

            // Act
            await Assert.ThrowsAsync<Exception>(async () => await adapter.PublishMessageAsync(payload, "queue"));

            // Should still allow reuse after error (semaphore released)
            _channel.BasicPublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<BasicProperties>(), Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
                    .Returns(ValueTask.CompletedTask);

            await adapter.PublishMessageAsync(payload, "queue"); // should NOT deadlock
        }

        class SampleMessage
        {
            public string Text { get; set; }

            public static SampleMessage Instance => new SampleMessage { Text = "test" };
        }
    }
}
