using global::Azure.Messaging.EventHubs;
using global::Azure.Messaging.EventHubs.Producer;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Pigeon.Messaging.Azure.EventHub.Producing;
using Pigeon.Messaging.Contracts;

namespace Pigeon.Messaging.Azure.EventHub.Tests.Producing
{
    public class EventHubProducingAdapterTests
    {
        //TODO: Fix and enable tests

        //[Fact]
        //public async Task PublishMessageAsync_Should_Send_Message_Successfully()
        //{
        //    // Arrange
        //    var provider = Substitute.For<IEventHubProvider>();
        //    var logger = Substitute.For<ILogger<EventHubProducingAdapter>>();
        //    var serializer = Substitute.For<ISerializer>();
        //    var producer = Substitute.For<EventHubProducerClient>();
        //    var eventBatch = Substitute.For<EventDataBatch>();

        //    serializer.Serialize(Arg.Any<object>()).Returns("{\"message\":\"test\"}");
        //    provider.GetProducer("test-hub").Returns(producer);
        //    producer.CreateBatchAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(eventBatch));
        //    eventBatch.TryAdd(Arg.Any<EventData>()).Returns(true);
        //    producer.SendAsync(Arg.Any<EventDataBatch>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        //    var payload = new WrappedPayload<TestMessage>
        //    {
        //        Message = new TestMessage { Content = "Test" },
        //        Domain = "test-domain",
        //        MessageVersion = new SemanticVersion(1, 0, 0),
        //        CreatedOnUtc = DateTimeOffset.UtcNow,
        //        Metadata = new Dictionary<string, object>()
        //    };

        //    var adapter = new EventHubProducingAdapter(provider, serializer, logger);

        //    // Act
        //    await adapter.PublishMessageAsync(payload, "test-hub");

        //    // Assert
        //    await producer.Received(1).CreateBatchAsync(Arg.Any<CancellationToken>());
        //    eventBatch.Received(1).TryAdd(Arg.Any<EventData>());
        //    await producer.Received(1).SendAsync(eventBatch, Arg.Any<CancellationToken>());

        //    // Verificar el logging
        //    logger.Received().Log(
        //        LogLevel.Information,
        //        Arg.Any<EventId>(),
        //        Arg.Is<object>(v => v.ToString().Contains("EventHub: Message published to hub") && v.ToString().Contains("test-hub")),
        //        Arg.Any<Exception>(),
        //        Arg.Any<Func<object, Exception, string>>());
        //}

        //[Fact]
        //public async Task PublishMessageAsync_Should_Throw_When_Event_Too_Large()
        //{
        //    // Arrange
        //    var provider = Substitute.For<IEventHubProvider>();
        //    var logger = Substitute.For<ILogger<EventHubProducingAdapter>>();
        //    var serializer = Substitute.For<ISerializer>();
        //    var producer = Substitute.For<EventHubProducerClient>();
        //    var eventBatch = Substitute.For<EventDataBatch>();

        //    serializer.SerializeAsBytes(Arg.Any<object>()).Returns(System.Text.Encoding.UTF8.GetBytes("{\"message\":\"test\"}"));
        //    provider.GetProducer("test-hub").Returns(producer);
        //    producer.CreateBatchAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(eventBatch));
        //    eventBatch.TryAdd(Arg.Any<EventData>()).Returns(false); // Event too large

        //    var payload = new WrappedPayload<TestMessage>
        //    {
        //        Message = new TestMessage { Content = "Test" },
        //        Domain = "test-domain",
        //        MessageVersion = new SemanticVersion(1, 0, 0),
        //        CreatedOnUtc = DateTimeOffset.UtcNow,
        //        Metadata = new Dictionary<string, object>()
        //    };

        //    var adapter = new EventHubProducingAdapter(provider, serializer, logger);

        //    // Act & Assert
        //    var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => 
        //        await adapter.PublishMessageAsync(payload, "test-hub"));

        //    Assert.Contains("too large to fit in a batch", ex.Message);
        //}

        //[Fact]
        //public async Task PublishMessageAsync_Should_Set_Event_Properties_Correctly()
        //{
        //    // Arrange
        //    var provider = Substitute.For<IEventHubProvider>();
        //    var logger = Substitute.For<ILogger<EventHubProducingAdapter>>();
        //    var serializer = Substitute.For<ISerializer>();
        //    var producer = Substitute.For<EventHubProducerClient>();
        //    var eventBatch = Substitute.For<EventDataBatch>();

        //    var payload = new WrappedPayload<TestMessage>
        //    {
        //        Message = new TestMessage { Content = "Test" },
        //        Domain = "test-domain",
        //        MessageVersion = new SemanticVersion(1, 0, 0),
        //        CreatedOnUtc = DateTimeOffset.Parse("2024-01-01T00:00:00Z"),
        //        Metadata = new Dictionary<string, object>()
        //    };

        //    serializer.SerializeAsBytes(Arg.Any<object>()).Returns(System.Text.Encoding.UTF8.GetBytes("{\"message\":\"test\"}"));
        //    provider.GetProducer("test-hub").Returns(producer);
        //    producer.CreateBatchAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(eventBatch));
        //    eventBatch.TryAdd(Arg.Any<EventData>()).Returns(true);
        //    producer.SendAsync(Arg.Any<EventDataBatch>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        //    var adapter = new EventHubProducingAdapter(provider, serializer, logger);

        //    // Act
        //    await adapter.PublishMessageAsync(payload, "test-hub");

        //    // Assert - Verificar que el evento fue agregado al batch con las propiedades correctas
        //    eventBatch.Received(1).TryAdd(Arg.Is<EventData>(ed =>
        //        ed.Properties.ContainsKey("Domain") &&
        //        ed.Properties["Domain"].ToString() == "test-domain" &&
        //        ed.Properties.ContainsKey("MessageVersion") &&
        //        ed.Properties["MessageVersion"].ToString() == "1.0.0" &&
        //        ed.Properties.ContainsKey("CreatedOnUtc")));
        //}

        [Fact]
        public async Task PublishMessageAsync_Should_LogError_And_Throw_On_Exception()
        {
            // Arrange
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();
            var producer = Substitute.For<EventHubProducerClient>();
            
            serializer.Serialize(Arg.Any<string>()).Returns("{\"message\":\"test\"}");
            provider.GetProducer("test-hub").Returns(producer);
            
            var exception = new Exception("Publishing failed");
            producer.CreateBatchAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromException<EventDataBatch>(exception));
            
            var payload = new WrappedPayload<TestMessage>
            {
                Message = new TestMessage { Content = "Test" },
                Domain = "test-domain",
                MessageVersion = new SemanticVersion(1, 0, 0),
                CreatedOnUtc = DateTimeOffset.UtcNow,
                Metadata = new Dictionary<string, object>()
            };
            
            var adapter = new EventHubProducingAdapter(provider, serializer, logger);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(async () => 
                await adapter.PublishMessageAsync(payload, "test-hub"));
            
            Assert.Equal("Publishing failed", ex.Message);
            
            // Verificar el logging de error
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("Error while publishing message using Azure Event Hub Adapter")),
                Arg.Is<Exception>(e => e.Message == "Publishing failed"),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Dependencies()
        {
            // Arrange
            var provider = Substitute.For<IEventHubProvider>();
            var logger = Substitute.For<ILogger<EventHubProducingAdapter>>();
            var serializer = Substitute.For<ISerializer>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubProducingAdapter(null, serializer, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubProducingAdapter(provider, null, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventHubProducingAdapter(provider, serializer, null));
        }

        private class TestMessage
        {
            public string Content { get; set; }
        }
    }
}