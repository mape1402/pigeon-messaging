using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid.Consuming;
using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;
using System.Text.Json;

namespace Pigeon.Messaging.Azure.EventGrid.Tests.Consuming
{
    public class EventGridConsumingAdapterTests
    {
        [Fact]
        public async Task StartConsumeAsync_Should_Create_Subscriptions_For_All_Topics()
        {
            // Arrange
            var topics = new[] { "topic1", "topic2" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var subscription1 = Substitute.For<IEventGridSubscription>();
            var subscription2 = Substitute.For<IEventGridSubscription>();
            
            provider.CreateSubscription("topic1").Returns(subscription1);
            provider.CreateSubscription("topic2").Returns(subscription2);
            
            subscription1.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            subscription2.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert
            await subscription1.Received(1).StartAsync(Arg.Any<CancellationToken>());
            await subscription2.Received(1).StartAsync(Arg.Any<CancellationToken>());
            
            // Verificar el logging
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("AzureEventGridConsumingAdapter has been initialized")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task StopConsumeAsync_Should_Stop_All_Subscriptions()
        {
            // Arrange
            var topics = new[] { "topic1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var subscription = Substitute.For<IEventGridSubscription>();
            provider.CreateSubscription("topic1").Returns(subscription);
            
            subscription.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            subscription.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            subscription.DisposeAsync().Returns(ValueTask.CompletedTask);
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();
            await adapter.StopConsumeAsync();

            // Assert
            await subscription.Received(1).StopAsync(Arg.Any<CancellationToken>());
            await subscription.Received(1).DisposeAsync();
            
            // Verificar el logging
            logger.Received().Log(
                LogLevel.Information,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("AzureEventGridConsumingAdapter has been stopped gracefully")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public async Task StartConsumeAsync_Should_Handle_Duplicate_Subscription_Creation()
        {
            // Arrange
            var topics = new[] { "topic1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var subscription = Substitute.For<IEventGridSubscription>();
            provider.CreateSubscription("topic1").Returns(subscription);
            subscription.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            subscription.DisposeAsync().Returns(ValueTask.CompletedTask);
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();
            await adapter.StartConsumeAsync(); // Second call should handle gracefully

            // Assert
            provider.Received().CreateSubscription("topic1"); // Called at least once
            await subscription.Received().StartAsync(Arg.Any<CancellationToken>()); // At least once
        }

        [Fact]
        public async Task StartConsumeAsync_Should_Log_Error_For_Subscription_Creation_Error()
        {
            // Arrange
            var topics = new[] { "topic1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var subscription = Substitute.For<IEventGridSubscription>();
            provider.CreateSubscription("topic1").Returns(subscription);
            subscription.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new Exception("Start failed")));
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);

            // Act
            await adapter.StartConsumeAsync();

            // Assert - Verificar el logging de error
            logger.Received().Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Is<object>(v => v.ToString().Contains("Error starting subscription for topic")),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception, string>>());
        }

        [Fact]
        public void Constructor_Should_Throw_ArgumentNullException_For_Null_Dependencies()
        {
            // Arrange
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(null, provider, options, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(configurator, null, options, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(configurator, provider, null, logger));
            
            Assert.Throws<ArgumentNullException>(() => 
                new EventGridConsumingAdapter(configurator, provider, options, null));
        }

        [Fact]
        public async Task MessageConsumed_Event_Should_Be_Raised_When_CloudEvent_Received()
        {
            // Arrange
            var topics = new[] { "topic1" };
            var configurator = Substitute.For<IConsumingConfigurator>();
            var provider = Substitute.For<IEventGridProvider>();
            var logger = Substitute.For<ILogger<EventGridConsumingAdapter>>();
            var options = Options.Create(new GlobalSettings { Domain = "test-domain" });
            
            configurator.GetAllTopics().Returns(topics);
            
            var subscription = Substitute.For<IEventGridSubscription>();
            provider.CreateSubscription("topic1").Returns(subscription);
            subscription.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            
            var adapter = new EventGridConsumingAdapter(configurator, provider, options, logger);
            MessageConsumedEventArgs capturedEventArgs = null;
            adapter.MessageConsumed += (sender, args) => capturedEventArgs = args;

            // Act
            await adapter.StartConsumeAsync();

            var payload = new TestMessage("hello-message");

            // Simulate cloud event received
            var eventGridEvent = new global::Azure.Messaging.EventGrid.EventGridEvent(
                subject: "topic1",
                eventType: "test.event",
                dataVersion: "1.0",
                data: payload);
            
            var eventArgs = new CloudEventReceivedEventArgs(eventGridEvent);
            subscription.CloudEventReceived += Raise.EventWith(subscription, eventArgs);

            // Assert
            Assert.NotNull(capturedEventArgs);
            Assert.Equal("topic1", capturedEventArgs.Topic);
            Assert.Equal(payload, JsonSerializer.Deserialize<TestMessage>(capturedEventArgs.RawPayload));
        }

        private record TestMessage(string data);
    }
}