namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using NSubstitute;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Contracts;
    using System;
    using System.Collections.Concurrent;
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
            var serializer = Substitute.For<ISerializer>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

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
            services.AddSingleton(serializer);

            var serviceProvider = services.BuildServiceProvider();

            var dispatcher = new ConsumingDispatcher(serviceProvider);

            await dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None);

            await interceptor.Received(1).Intercept(Arg.Any<ConsumeContext>());
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task DispatchAsync_Should_Resolve_Handler_By_Subscription()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var interceptor = Substitute.For<IConsumeInterceptor>();
            var serializer = Substitute.For<ISerializer>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            ConsumeContext capturedContext = null;
            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                capturedContext = ctx;
                return Task.CompletedTask;
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default,
                Subscription = "billing"
            };

            consumingConfigurator
                .GetConfiguration("test-topic", SemanticVersion.Default, "billing")
                .Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddScoped(p => interceptor);
            services.AddSingleton(serializer);

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider());

            await dispatcher.DispatchAsync("test-topic", "billing", new RawPayload(ValidJson), CancellationToken.None);

            Assert.NotNull(capturedContext);
            Assert.Equal("billing", capturedContext.Subscription);
            await interceptor.Received(1).Intercept(Arg.Any<ConsumeContext>());
        }

        [Fact]
        public async Task DispatchAsync_Should_Expose_Current_Context_Through_Accessor()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            ConsumeContext interceptorContext = null;
            ConsumeContext handlerContext = null;

            var interceptor = new AccessorConsumeInterceptor(accessor => interceptorContext = accessor.ConsumeContext);
            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                var accessor = ctx.Services.GetRequiredService<IConsumeContextAccessor>();
                handlerContext = accessor.ConsumeContext;
                return Task.CompletedTask;
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default
            };

            consumingConfigurator.GetConfiguration("test-topic", SemanticVersion.Default).Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddScoped<IConsumeInterceptor>(_ => interceptor);
            services.AddSingleton(serializer);
            services.AddSingleton<ConsumeContextAccessor>();
            services.AddSingleton<IConsumeContextAccessor>(provider => provider.GetRequiredService<ConsumeContextAccessor>());

            var serviceProvider = services.BuildServiceProvider();
            var accessor = serviceProvider.GetRequiredService<IConsumeContextAccessor>();
            var dispatcher = new ConsumingDispatcher(serviceProvider);

            Assert.Null(accessor.ConsumeContext);

            await dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None);

            Assert.NotNull(interceptorContext);
            Assert.Same(interceptorContext, handlerContext);
            Assert.Null(accessor.ConsumeContext);
        }

        [Fact]
        public async Task DispatchAsync_Should_Clear_Accessor_When_Handler_Throws()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                throw new InvalidOperationException("boom");
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default
            };

            consumingConfigurator.GetConfiguration("test-topic", SemanticVersion.Default).Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddSingleton(serializer);
            services.AddSingleton<ConsumeContextAccessor>();
            services.AddSingleton<IConsumeContextAccessor>(provider => provider.GetRequiredService<ConsumeContextAccessor>());

            var serviceProvider = services.BuildServiceProvider();
            var accessor = serviceProvider.GetRequiredService<IConsumeContextAccessor>();
            var dispatcher = new ConsumingDispatcher(serviceProvider);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None));

            Assert.Null(accessor.ConsumeContext);
        }

        [Fact]
        public async Task DispatchAsync_Should_Isolate_Accessor_Between_Concurrent_Messages()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            var observedSubscriptions = new ConcurrentBag<string>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            ConsumerConfiguration<TestMessage> CreateConfiguration(string subscription)
                => new(async (ctx, message) =>
                {
                    var accessor = ctx.Services.GetRequiredService<IConsumeContextAccessor>();
                    observedSubscriptions.Add(accessor.ConsumeContext.Subscription);
                    await Task.Delay(50);
                    observedSubscriptions.Add(accessor.ConsumeContext.Subscription);
                })
                {
                    Topic = "test-topic",
                    Version = SemanticVersion.Default,
                    Subscription = subscription
                };

            consumingConfigurator
                .GetConfiguration("test-topic", SemanticVersion.Default, "billing")
                .Returns(CreateConfiguration("billing"));
            consumingConfigurator
                .GetConfiguration("test-topic", SemanticVersion.Default, "audit")
                .Returns(CreateConfiguration("audit"));

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddSingleton(serializer);
            services.AddSingleton<ConsumeContextAccessor>();
            services.AddSingleton<IConsumeContextAccessor>(provider => provider.GetRequiredService<ConsumeContextAccessor>());

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = new ConsumingDispatcher(serviceProvider);

            await Task.WhenAll(
                dispatcher.DispatchAsync("test-topic", "billing", new RawPayload(ValidJson), CancellationToken.None),
                dispatcher.DispatchAsync("test-topic", "audit", new RawPayload(ValidJson), CancellationToken.None));

            Assert.Equal(2, observedSubscriptions.Count(x => x == "billing"));
            Assert.Equal(2, observedSubscriptions.Count(x => x == "audit"));
            Assert.Null(serviceProvider.GetRequiredService<IConsumeContextAccessor>().ConsumeContext);
        }

        private class TestMessage { }

        private sealed class AccessorConsumeInterceptor : IConsumeInterceptor
        {
            private readonly Action<IConsumeContextAccessor> _capture;

            public AccessorConsumeInterceptor(Action<IConsumeContextAccessor> capture)
            {
                _capture = capture;
            }

            public ValueTask Intercept(ConsumeContext context, CancellationToken cancellationToken = default)
            {
                _capture(context.Services.GetRequiredService<IConsumeContextAccessor>());
                return ValueTask.CompletedTask;
            }
        }
    }
}
