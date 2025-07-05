namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using NSubstitute;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class ConsumingDispatcherTests
    {
        private const string ValidJson = @"{
            ""Domain"": ""test-domain"",
            ""MessageVersion"": ""1.0.0"",
            ""CreatedOnUtc"": ""2024-01-01T00:00:00Z"",
            ""Message"": { ""Text"": ""Hello"" },
            ""Metadata"": { ""Key"": { ""Prop"": ""Value"" } }
        }";

        [Fact]
        public async Task DispatchAsync_ThrowsArgumentNullException_WhenTopicIsNullOrEmpty()
        {
            var serviceProvider = Substitute.For<IServiceProvider>();
            var dispatcher = new ConsumingDispatcher(serviceProvider);

            await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync(null, new RawPayload(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync("", new RawPayload(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(() => dispatcher.DispatchAsync("   ", new RawPayload(), CancellationToken.None));
        }

        [Fact]
        public async Task DispatchAsync_InvokesHandlerAndInterceptors()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var interceptor = Substitute.For<IConsumeInterceptor>();

            var handlerCalled = false;

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default
            };

            consumingConfigurator.GetConfiguration("test-topic", SemanticVersion.Default).Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddScoped(p => interceptor);

            var serviceProvider = services.BuildServiceProvider();

            var dispatcher = new ConsumingDispatcher(serviceProvider);

            await dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None);

            await interceptor.Received(1).Intercept(Arg.Any<ConsumeContext>());
            Assert.True(handlerCalled);
        }

        private class TestMessage { }
    }
}
