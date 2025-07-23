using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Azure.ServiceBus.Producing;
using Pigeon.Messaging.Contracts;

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
            var sender = Substitute.For<ServiceBusSender>();
            provider.GetSender("topic").Returns(sender);
            var payload = new WrappedPayload<string> { Message = "test" };
            var adapter = new ServiceBusProducingAdapter(provider, logger);

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
            var sender = Substitute.For<ServiceBusSender>();
            provider.GetSender("topic").Returns(sender);
            var payload = new WrappedPayload<string> { Message = "test" };
            var adapter = new ServiceBusProducingAdapter(provider, logger);
            var exception = new Exception("fail");
            sender.SendMessageAsync(Arg.Any<ServiceBusMessage>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw exception);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () => await adapter.PublishMessageAsync(payload, "topic"));
            Assert.Equal("fail", ex.Message);
        }
    }
}
