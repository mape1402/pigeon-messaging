using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;
using Pigeon.Messaging.Contracts;

namespace Pigeon.Messaging.Tests.Consuming.Configuration
{
    public class ConsumingConfiguratorTests
    {
        private readonly ConsumingConfigurator _configurator;

        public ConsumingConfiguratorTests()
        {
            _configurator = new ConsumingConfigurator();
        }

        private static Task DummyHandler(ConsumeContext ctx, DummyMessage msg) => Task.CompletedTask;

        private record DummyMessage;

        [Fact]
        public void AddConsumer_Should_Add_Consumer_When_Valid()
        {
            // Arrange
            var topic = "test-topic";
            var version = new SemanticVersion(1, 0, 0);

            // Act
            var result = _configurator.AddConsumer<DummyMessage>(topic, version, DummyHandler);

            // Assert
            Assert.NotNull(result);
            var config = _configurator.GetConfiguration(topic, version);
            Assert.NotNull(config);
            Assert.Equal(topic, config.Topic);
            Assert.Equal(version, config.Version);
        }

        [Fact]
        public void AddConsumer_Should_Throw_When_Duplicate()
        {
            var topic = "test-topic";
            var version = SemanticVersion.Default;

            _configurator.AddConsumer<DummyMessage>(topic, version, DummyHandler);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _configurator.AddConsumer<DummyMessage>(topic, version, DummyHandler));

            Assert.Contains("already registered", ex.Message);
        }

        [Fact]
        public void AddConsumer_Should_Throw_When_Handler_Is_Null()
        {
            var topic = "test-topic";
            Assert.Throws<ArgumentNullException>(() =>
                _configurator.AddConsumer<DummyMessage>(topic, null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void AddConsumer_Should_Throw_When_Topic_Invalid(string topic)
        {
            Assert.Throws<ArgumentException>(() =>
                _configurator.AddConsumer<DummyMessage>(topic, DummyHandler));
        }

        [Fact]
        public void RemoveConsumer_Should_Remove_Consumer()
        {
            var topic = "remove-topic";
            _configurator.AddConsumer<DummyMessage>(topic, DummyHandler);

            _configurator.RemoveConsumer(topic);

            Assert.Throws<InvalidOperationException>(() => _configurator.GetConfiguration(topic));
        }

        [Fact]
        public void RemoveConsumer_Should_Not_Throw_If_Not_Exists()
        {
            var topic = "non-existent";
            var ex = Record.Exception(() => _configurator.RemoveConsumer(topic));
            Assert.Null(ex);
        }

        [Fact]
        public void GetConfiguration_Should_Throw_If_Not_Exists()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _configurator.GetConfiguration("unknown-topic"));
        }

        [Fact]
        public void GetAllTopics_Should_Return_Registered_Topics()
        {
            _configurator.AddConsumer<DummyMessage>("a", DummyHandler);
            _configurator.AddConsumer<DummyMessage>("b", DummyHandler);

            var topics = _configurator.GetAllTopics().ToList();

            Assert.Contains("a", topics);
            Assert.Contains("b", topics);
        }
    }

}