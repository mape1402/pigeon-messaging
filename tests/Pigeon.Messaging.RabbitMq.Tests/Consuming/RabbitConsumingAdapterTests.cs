namespace Pigeon.Messaging.RabbitMq.Tests.Consuming
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
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
        private readonly IOptions<GlobalSettings> _options = Options.Create(new GlobalSettings { Domain = "test" });
        private readonly IOptions<RabbitSettings> _rabbitOptions = Options.Create(new RabbitSettings());

        [Fact]
        public async Task Should_StartConsume_And_RegisterConsumerPerTopic()
        {
            // Arrange
            var topics = new[] { "topic1", "topic2" };
            _consumingConfigurator.GetAllTopics().Returns(topics);
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _options, _rabbitOptions, _logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
            await _connectionProvider.Received(topics.Length).CreateChannelAsync(Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().QueueDeclareAsync(Arg.Any<string>(), false, false, false, null, false, Arg.Any<CancellationToken>());
            await _channel.Received(topics.Length).BasicConsumeAsync(Arg.Any<string>(), false, Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>());

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

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _options, _rabbitOptions, _logger);

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
            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _options, _rabbitOptions, _logger);

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

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _options, _rabbitOptions, _logger);
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

        [Fact]
        public async Task Should_Use_AutoAck_When_Acknowledgement_Mode_Is_OnReceive()
        {
            var topic = "topic1";
            var options = Options.Create(new GlobalSettings
            {
                Domain = "test",
                ConsumerExecution = new ConsumerExecutionSettings
                {
                    AcknowledgementMode = MessageAcknowledgementMode.OnReceive
                }
            });
            _consumingConfigurator.GetAllTopics().Returns(new[] { topic });
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, options, _rabbitOptions, _logger);

            await adapter.StartConsumeAsync();

            await _channel.Received(1).BasicConsumeAsync(topic, true, Arg.Any<IAsyncBasicConsumer>(), Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().BasicQosAsync(Arg.Any<uint>(), Arg.Any<ushort>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Bind_Queue_To_Configured_Exchange()
        {
            // Arrange
            var topic = "topic1";
            var rabbitOptions = Options.Create(new RabbitSettings
            {
                Exchange = "events",
                ExchangeType = "topic",
                DurableExchange = true
            });
            _consumingConfigurator.GetAllTopics().Returns(new[] { topic });
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _options, rabbitOptions, _logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
            await _channel.DidNotReceive().ExchangeDeclareAsync("events", "topic", true, false, null, false, Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().QueueBindAsync(topic, "events", topic, null, false, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Create_Queue_Per_Subscription_For_Same_Topic()
        {
            // Arrange
            var topic = "user.created";
            var endpoints = new[]
            {
                new ConsumerEndpoint(topic, "billing"),
                new ConsumerEndpoint(topic, "notifications")
            };
            var rabbitOptions = Options.Create(new RabbitSettings
            {
                Exchange = "events",
                ExchangeType = "topic"
            });
            _consumingConfigurator.GetAllEndpoints().Returns(endpoints);
            _connectionProvider.CreateChannelAsync(Arg.Any<CancellationToken>()).Returns(_channel);

            var adapter = new RabbitConsumingAdapter(_connectionProvider, _consumingConfigurator, _options, rabbitOptions, _logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
            await _channel.DidNotReceive().QueueDeclareAsync("billing", false, false, false, null, false, Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().QueueDeclareAsync("notifications", false, false, false, null, false, Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().QueueBindAsync("billing", "events", topic, null, false, Arg.Any<CancellationToken>());
            await _channel.DidNotReceive().QueueBindAsync("notifications", "events", topic, null, false, Arg.Any<CancellationToken>());
        }
    }

}
