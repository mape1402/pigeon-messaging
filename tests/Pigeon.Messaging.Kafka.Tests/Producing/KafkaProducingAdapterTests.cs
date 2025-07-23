using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Kafka.Producing;
using Xunit;

namespace Pigeon.Messaging.Kafka.Tests.Producing
{
    public class KafkaProducingAdapterTests
    {
        [Fact]
        public async Task PublishMessageAsync_Should_Call_Producer_And_Log_Info()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            var logger = Substitute.For<ILogger<KafkaProducingAdapter>>();
            var producer = Substitute.For<IKafkaProducer<SampleMessage>>();
            var payload = new WrappedPayload<SampleMessage> { Message = new SampleMessage() };
            var topic = "test-topic";
            var deliveryResult = new Confluent.Kafka.DeliveryResult<Confluent.Kafka.Null, WrappedPayload<SampleMessage>>
            {
                Offset = new Confluent.Kafka.Offset(1),
                Partition = new Confluent.Kafka.Partition(2)
            };
            producer.PublishAsync(payload, topic, Arg.Any<CancellationToken>()).Returns(Task.FromResult(deliveryResult));
            serviceProvider.GetService<IKafkaProducer<SampleMessage>>().Returns(producer);
            var adapter = new KafkaProducingAdapter(serviceProvider, logger);

            // Act
            await adapter.PublishMessageAsync(payload, topic);

            // Assert
            await producer.Received(1).PublishAsync(payload, topic, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishMessageAsync_Should_LogError_And_Throw_On_Exception()
        {
            // Arrange
            var serviceProvider = Substitute.For<IServiceProvider>();
            var logger = Substitute.For<ILogger<KafkaProducingAdapter>>();
            var producer = Substitute.For<IKafkaProducer<SampleMessage>>();
            var payload = new WrappedPayload<SampleMessage> { Message = new SampleMessage() };
            var topic = "test-topic";
            var exception = new Exception("fail");
            producer.PublishAsync(payload, topic, Arg.Any<CancellationToken>()).Returns<Task<Confluent.Kafka.DeliveryResult<Confluent.Kafka.Null, WrappedPayload<SampleMessage>>>>(_ => throw exception);
            serviceProvider.GetService<IKafkaProducer<SampleMessage>>().Returns(producer);
            var adapter = new KafkaProducingAdapter(serviceProvider, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () => await adapter.PublishMessageAsync(payload, topic));
            Assert.Equal("fail", ex.Message);
        }
    }

    // Dummy message for testing
    public class SampleMessage { }
}
