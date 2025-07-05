namespace Pigeon.Messaging.RabbitMq.Tests.Consuming
{
    using Microsoft.Extensions.Logging;
    using NSubstitute;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Rabbit;
    using Pigeon.Messaging.Rabbit.Consuming;
    using RabbitMQ.Client;
    using System;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class RabbitConsumingAdapterTests
    {
        private readonly IConnectionProvider _connectionProvider = Substitute.For<IConnectionProvider>();
        private readonly IConsumingConfigurator _consumingConfigurator = Substitute.For<IConsumingConfigurator>();
        private readonly ILogger<RabbitConsumingAdapter> _logger = Substitute.For<ILogger<RabbitConsumingAdapter>>();

        private readonly IChannel _channel = Substitute.For<IChannel>();

        [Fact]
        public async Task Should_StartConsume_And_RegisterConsumerPerTopic()
        {
            // Arrange
            var topics = new[] { "topic1", "topic2" };
            _consumingConfigurator.GetAllTopics().Returns(topics);
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
            await _connectionProvider.Received(topics.Length).CreateChannelAsync(Arg.Any<CancellationToken>());
            await _channel.Received(topics.Length).QueueDeclareAsync(Arg.Any<string>(), false, false, false, null, false, Arg.Any<CancellationToken>());
            await _channel.Received(topics.Length).BasicConsumeAsync(Arg.Any<string>(), true, Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>());

            _logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task Should_Not_AddDuplicateConsumers()
        {
            // Arrange
            var topic = "duplicateTopic";
            _consumingConfigurator.GetAllTopics().Returns(new[] { topic, topic });
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert: TryAdd only succeeds once, second should Dispose the channel
            await _channel.Received(1).DisposeAsync();
            _logger.Received().Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public void Should_Invoke_MessageConsumed_WhenMessageReceived()
        {
            // Arrange
            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _logger);

            bool eventRaised = false;
            adapter.MessageConsumed += (s, e) =>
            {
                eventRaised = true;
                Assert.Equal("myTopic", e.Topic);
                Assert.Equal("myMessage", e.RawPayload);
            };

            // Act: Simulate consumer received event
            var adapterType = typeof(RabbitConsumingAdapter);   

            //var eventInfo = adapterType
            //    .GetEvent(nameof(adapter.MessageConsumed));

            var fieldInfo = adapterType
                .GetField(nameof(adapter.MessageConsumed),
                          BindingFlags.Instance | BindingFlags.NonPublic);

            var eventDelegate = (MulticastDelegate)fieldInfo.GetValue(adapter);

            foreach (var handler in eventDelegate.GetInvocationList())
            {
                handler.DynamicInvoke(adapter, new MessageConsumedEventArgs("myTopic", "myMessage"));
            }

            // Assert
            Assert.True(eventRaised);
        }

        [Fact]
        public async Task Should_StopConsumeAsync_DisposeAndClearChannels()
        {
            // Arrange
            _channel.IsOpen.Returns(true);
            _consumingConfigurator.GetAllTopics().Returns(new[] { "topic1" });
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _logger);
            await adapter.StartConsumeAsync();

            // Act
            await adapter.StopConsumeAsync();

            // Assert
            await _channel.Received(1).CloseAsync(Arg.Any<CancellationToken>());
            await _channel.Received(1).DisposeAsync();
            _logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }
    }

}
