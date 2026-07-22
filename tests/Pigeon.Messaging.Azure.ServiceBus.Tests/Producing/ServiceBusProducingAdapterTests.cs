using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Producing;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;

namespace Pigeon.Messaging.Azure.ServiceBus.Tests.Producing
{
    public class ServiceBusProducingAdapterTests
    {
        [Fact]
        public async Task PublishMessageAsync_Should_Send_Message()
        {
            // Arrange
            var provider = Substitute.For<IServiceBusProvider>();
            var logger = Substitute.For<ILogger<ServiceBusProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var sender = Substitute.For<ServiceBusSender>();
            provider.GetSender("topic").Returns(sender);
            var payload = new WrappedPayload<string> { Message = "test" };
            var adapter = new ServiceBusProducingAdapter(provider, serializer, logger);

            // Act
            await adapter.PublishMessageAsync(payload, "topic");

            // Assert
            await sender.Received(1).SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishMessageAsync_Should_LogError_And_Throw_On_Exception()
        {
            // Arrange
            var provider = Substitute.For<IServiceBusProvider>();
            var logger = Substitute.For<ILogger<ServiceBusProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var sender = Substitute.For<ServiceBusSender>();
            provider.GetSender("topic").Returns(sender);
            var payload = new WrappedPayload<string> { Message = "test" };
            var adapter = new ServiceBusProducingAdapter(provider, serializer, logger);
            var exception = new Exception("fail");
            sender.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw exception);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () => await adapter.PublishMessageAsync(payload, "topic"));
            Assert.Equal("fail", ex.Message);
        }

        [Fact]
        public async Task PublishRawMessageAsync_Should_Serialize_Raw_Message()
        {
            // Arrange
            var provider = Substitute.For<IServiceBusProvider>();
            var logger = Substitute.For<ILogger<ServiceBusProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var sender = Substitute.For<ServiceBusSender>();
            provider.GetSender("topic").Returns(sender);
            var message = "test";
            var adapter = new ServiceBusProducingAdapter(provider, serializer, logger);

            // Act
            await adapter.PublishRawMessageAsync(message, "topic");

            // Assert
            serializer.Received(1).Serialize(message);
            serializer.DidNotReceive().Serialize(Arg.Any<WrappedPayload<string>>());
            await sender.Received(1).SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishMessageAsync_Should_Add_Route_Properties()
        {
            // Arrange
            var provider = Substitute.For<IServiceBusProvider>();
            var logger = Substitute.For<ILogger<ServiceBusProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            serializer.Serialize(Arg.Any<object>()).Returns("{}");
            var sender = Substitute.For<ServiceBusSender>();
            provider.GetSender("user.created").Returns(sender);
            var payload = new WrappedPayload<string> { Message = "test" };
            var route = PublishingRoute.ForExchange("events", "user.created");
            var adapter = new ServiceBusProducingAdapter(provider, serializer, logger);

            // Act
            await adapter.PublishMessageAsync(payload, route);

            // Assert
            provider.Received(1).GetSender("user.created");
            await sender.Received(1).SendMessageAsync(
                Arg.Is<ServiceBusMessage>(message =>
                    message.ApplicationProperties["RoutingKey"].ToString() == "user.created" &&
                    message.ApplicationProperties["Exchange"].ToString() == "events"),
                Arg.Any<CancellationToken>());
        }
    }
}
