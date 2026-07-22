using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid;
using Pigeon.Messaging.Azure.EventGrid.Producing;
using Pigeon.Messaging.Contracts;
using Pigeon.Messaging.Producing;
using global::Azure.Messaging.EventGrid;

namespace Pigeon.Messaging.Azure.EventGrid.Tests.Producing
{
    public class EventGridProducingAdapterTests
    {
        [Fact]
        public async Task PublishMessageAsync_Should_Send_Message_Successfully()
        {
            // Arrange
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            var publisher = Substitute.For<IEventGridPublisher>();
            
            serializer.Serialize(Arg.Any<object>()).Returns("{\"message\":\"test\"}");
            provider.GetClient("test-topic").Returns(publisher);
            publisher.PublishCloudEventsAsync(Arg.Any<IEnumerable<EventGridEvent>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            
            var payload = new WrappedPayload<TestMessage>
            {
                Message = new TestMessage { Content = "Test" },
                Domain = "test-domain",
                MessageVersion = new SemanticVersion(1, 0, 0),
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object>()
            };
            
            var adapter = new EventGridProducingAdapter(provider, serializer, logger);

            // Act
            await adapter.PublishMessageAsync(payload, "test-topic");

            // Assert
            await publisher.Received(1).PublishCloudEventsAsync(
                Arg.Is<IEnumerable<EventGridEvent>>(events => events.Count() == 1),
                Arg.Any<CancellationToken>());
            
            // Verificar el logging
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("EventGrid: Message published to topic") && v.ToString().Contains("test-topic")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task PublishMessageAsync_Should_LogError_And_Throw_On_Exception()
        {
            // Arrange
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            var publisher = Substitute.For<IEventGridPublisher>();
            
            serializer.Serialize(Arg.Any<object>()).Returns("{\"message\":\"test\"}");
            provider.GetClient("test-topic").Returns(publisher);
            
            var exception = new Exception("Publishing failed");
            publisher.PublishCloudEventsAsync(Arg.Any<IEnumerable<EventGridEvent>>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException(exception));
            
            var payload = new WrappedPayload<TestMessage>
            {
                Message = new TestMessage { Content = "Test" },
                Domain = "test-domain",
                MessageVersion = new SemanticVersion(1, 0, 0),
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object>()
            };
            
            var adapter = new EventGridProducingAdapter(provider, serializer, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () => 
                await adapter.PublishMessageAsync(payload, "test-topic"));
            
            Assert.Equal("Publishing failed", ex.Message);
            
            // Verificar el logging de error
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("Error while publishing message using Azure Event Grid Adapter")),
                Arg.Is<Exception>(e => e.Message == "Publishing failed"),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task PublishRawMessageAsync_Should_Send_Raw_Message()
        {
            // Arrange
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            var publisher = Substitute.For<IEventGridPublisher>();
            var message = new TestMessage { Content = "Test" };

            serializer.Serialize(Arg.Any<object>()).Returns("{\"content\":\"Test\"}");
            provider.GetClient("test-topic").Returns(publisher);
            publisher.PublishCloudEventsAsync(Arg.Any<IEnumerable<EventGridEvent>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var adapter = new EventGridProducingAdapter(provider, serializer, logger);

            // Act
            await adapter.PublishRawMessageAsync(message, "test-topic");

            // Assert
            serializer.Received(1).Serialize(message);
            serializer.DidNotReceive().Serialize(Arg.Any<WrappedPayload<TestMessage>>());
            await publisher.Received(1).PublishCloudEventsAsync(
                Arg.Is<IEnumerable<EventGridEvent>>(events => events.Count() == 1),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PublishMessageAsync_Should_Use_Route_For_EventGrid_Event()
        {
            // Arrange
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            var publisher = Substitute.For<IEventGridPublisher>();
            var route = PublishingRoute.ForExchange("events", "user.created");

            serializer.Serialize(Arg.Any<object>()).Returns("{\"message\":\"test\"}");
            provider.GetClient("user.created").Returns(publisher);
            publisher.PublishCloudEventsAsync(Arg.Any<IEnumerable<EventGridEvent>>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var payload = new WrappedPayload<TestMessage>
            {
                Message = new TestMessage { Content = "Test" },
                Domain = "test-domain",
                MessageVersion = new SemanticVersion(1, 0, 0),
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object>()
            };
            var adapter = new EventGridProducingAdapter(provider, serializer, logger);

            // Act
            await adapter.PublishMessageAsync(payload, route);

            // Assert
            provider.Received(1).GetClient("user.created");
            await publisher.Received(1).PublishCloudEventsAsync(
                Arg.Is<IEnumerable<EventGridEvent>>(events =>
                    events.Single().Subject == "user.created" &&
                    events.Single().EventType == "user.created.1.0.0" &&
                    events.Single().Topic == "events"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Dependencies()
        {
            // Arrange
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridProducingAdapter(null, serializer, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridProducingAdapter(provider, null, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridProducingAdapter(provider, serializer, null));
        }

        private class TestMessage
        {
            public string Content { get; set; }
        }
    }
}
