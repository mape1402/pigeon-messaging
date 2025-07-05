namespace Pigeon.Messaging.RabbitMq.Tests
{
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Rabbit;
    using RabbitMQ.Client;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ConnectionProviderTests
    {
        private readonly IOptions<RabbitSettings> _options;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IConnectionFactory _factory;

        public ConnectionProviderTests()
        {
            _options = Substitute.For<IOptions<RabbitSettings>>();
            _options.Value.Returns(new RabbitSettings { Url = "amqp://guest:guest@localhost:5672" });

            _connection = Substitute.For<IConnection>();
            _channel = Substitute.For<IChannel>();

            _factory = Substitute.For<IConnectionFactory>();
            _factory.CreateConnectionAsync(Arg.Any<CancellationToken>()).Returns(_connection);
        }

        [Fact]
        public async Task Should_Create_New_Connection_When_None_Exists()
        {
            // Arrange
            var sut = new ConnectionProvider(_factory, _options);

            // Act
            var connection = await sut.CreateConnectionAsync();

            // Assert
            Assert.NotNull(connection);
            Assert.Equal(_connection, connection);
        }

        [Fact]
        public async Task Should_Reuse_Open_Connection()
        {
            // Arrange
            _connection.IsOpen.Returns(true);
            var sut = new ConnectionProvider(_factory, _options);
            sut.GetType().GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(sut, _connection);

            // Act
            var connection1 = await sut.CreateConnectionAsync();
            var connection2 = await sut.CreateConnectionAsync();

            // Assert: Should return same instance
            Assert.Same(connection1, connection2);
        }

        [Fact]
        public async Task Should_Create_Channel_On_Open_Connection()
        {
            // Arrange
            _connection.IsOpen.Returns(true);
            _connection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>()).Returns(_channel);

            var sut = new ConnectionProvider(_factory, _options);
            sut.GetType().GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(sut, _connection);

            // Act
            var channel = await sut.CreateChannelAsync();

            // Assert
            Assert.Equal(_channel, channel);
            await _connection.Received(1).CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_Call_Close_When_Disposing_If_Open()
        {
            // Arrange
            _connection.IsOpen.Returns(true);

            var sut = new ConnectionProvider(_factory, _options);
            sut.GetType().GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(sut, _connection);

            // Act
            await sut.DisposeAsync();

            // Assert
            await _connection.Received(1).CloseAsync();
            _connection.Received(1).Dispose();
        }

        [Fact]
        public async Task Should_Not_Close_When_Connection_Null()
        {
            // Arrange
            var sut = new ConnectionProvider(_factory, _options);

            // Act
            await sut.DisposeAsync();

            // Assert: Nothing to assert because connection is null, no exception should occur
        }
    }

}
