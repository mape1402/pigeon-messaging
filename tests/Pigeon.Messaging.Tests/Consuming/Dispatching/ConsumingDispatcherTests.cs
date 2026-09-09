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

        [Theory]
        [InlineData(PigeonConsumeDecision.AckAndSkip, 1, 0, 0)]
        [InlineData(PigeonConsumeDecision.Defer, 1, 0, 0)]
        [InlineData(PigeonConsumeDecision.Retry, 0, 1, 0)]
        [InlineData(PigeonConsumeDecision.Reject, 0, 0, 1)]
        public async Task DispatchAsync_Should_Apply_Consume_Decisions(
            PigeonConsumeDecision decision,
            int expectedCompleted,
            int expectedRetried,
            int expectedRejected)
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var handlerCalled = false;
            var completed = 0;
            var retried = 0;
            var rejected = 0;

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
            services.AddSingleton(serializer);
            services.AddScoped<IConsumeDecisionInterceptor>(_ => new FixedDecisionInterceptor(decision));

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider());

            await dispatcher.DispatchAsync(
                "test-topic",
                ConsumerEndpoint.DefaultSubscription,
                new RawPayload(ValidJson),
                _ =>
                {
                    completed++;
                    return Task.CompletedTask;
                },
                (_, _) => Task.CompletedTask,
                (_, _) =>
                {
                    retried++;
                    return Task.CompletedTask;
                },
                (_, _) =>
                {
                    rejected++;
                    return Task.CompletedTask;
                },
                ConsumeExecutionSource.BrokerDelivery,
                CancellationToken.None);

            Assert.False(handlerCalled);
            Assert.Equal(expectedCompleted, completed);
            Assert.Equal(expectedRetried, retried);
            Assert.Equal(expectedRejected, rejected);
        }

        [Fact]
        public async Task DispatchAsync_Should_Apply_Route_Specific_Consume_Decision()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            var registry = new PigeonRouteInterceptorRegistry();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var handlerCalled = false;
            var completed = 0;

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                handlerCalled = true;
                return Task.CompletedTask;
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default,
                Subscription = "billing"
            };

            registry.AddConsumeDecisionInterceptor(
                new PigeonRouteKey("test-topic", SemanticVersion.Default, "billing"),
                typeof(RouteAckInterceptor));

            consumingConfigurator.GetConfiguration("test-topic", SemanticVersion.Default, "billing").Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddSingleton(serializer);
            services.AddScoped<RouteAckInterceptor>();

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider(), registry);

            await dispatcher.DispatchAsync(
                "test-topic",
                "billing",
                new RawPayload(ValidJson),
                _ =>
                {
                    completed++;
                    return Task.CompletedTask;
                },
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                ConsumeExecutionSource.BrokerDelivery,
                CancellationToken.None);

            Assert.False(handlerCalled);
            Assert.Equal(1, completed);
        }

        [Fact]
        public async Task DispatchAsync_Should_Run_Global_Execution_Interceptor_Around_Handler()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            var events = new List<string>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                events.Add(ctx.Services.GetRequiredService<IConsumeContextAccessor>().ConsumeContext == ctx
                    ? "handler-context"
                    : "handler-no-context");
                return Task.CompletedTask;
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
            services.AddScoped<IConsumeExecutionInterceptor>(_ => new RecordingExecutionInterceptor(events, "global"));
            services.AddSingleton(new PigeonRouteInterceptorRegistry());
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();

            var serviceProvider = services.BuildServiceProvider();
            var dispatcher = new ConsumingDispatcher(serviceProvider);

            await dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None);

            Assert.Equal(
                new[] { "global-before", "handler-context", "global-after", "global-finally" },
                events);
            Assert.Null(serviceProvider.GetRequiredService<IConsumeContextAccessor>().ConsumeContext);
        }

        [Fact]
        public async Task DispatchAsync_Should_Run_Route_Execution_Interceptor_Around_Handler()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            var registry = new PigeonRouteInterceptorRegistry();
            var events = new List<string>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                events.Add("handler");
                return Task.CompletedTask;
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default,
                Subscription = "billing"
            };

            registry.AddConsumeExecutionInterceptor(
                new PigeonRouteKey("test-topic", SemanticVersion.Default, "billing"),
                typeof(RouteRecordingExecutionInterceptor));

            consumingConfigurator.GetConfiguration("test-topic", SemanticVersion.Default, "billing").Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddSingleton(serializer);
            services.AddSingleton(registry);
            services.AddSingleton(events);
            services.AddScoped<RouteRecordingExecutionInterceptor>();
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider(), registry);

            await dispatcher.DispatchAsync("test-topic", "billing", new RawPayload(ValidJson), CancellationToken.None);

            Assert.Equal(new[] { "route-before", "handler", "route-after", "route-finally" }, events);
        }

        [Fact]
        public async Task DispatchAsync_Should_Not_Run_Execution_Interceptor_When_Decision_Short_Circuits()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            var events = new List<string>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                events.Add("handler");
                return Task.CompletedTask;
            })
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default
            };

            consumingConfigurator.GetConfiguration("test-topic", SemanticVersion.Default).Returns(consumerConfig);

            var services = new ServiceCollection();
            services.AddSingleton(consumingConfigurator);
            services.AddSingleton(serializer);
            services.AddScoped<IConsumeDecisionInterceptor>(_ => new FixedDecisionInterceptor(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<IConsumeExecutionInterceptor>(_ => new RecordingExecutionInterceptor(events, "global"));
            services.AddSingleton(new PigeonRouteInterceptorRegistry());
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider());

            await dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None);

            Assert.Empty(events);
        }

        [Fact]
        public async Task DispatchAsync_Should_Run_Execution_Interceptor_Finally_And_Rethrow_When_Handler_Fails()
        {
            var consumingConfigurator = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            var events = new List<string>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var consumerConfig = new ConsumerConfiguration<TestMessage>((ctx, message) =>
            {
                events.Add("handler");
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
            services.AddScoped<IConsumeExecutionInterceptor>(_ => new RecordingExecutionInterceptor(events, "global"));
            services.AddSingleton(new PigeonRouteInterceptorRegistry());
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider());

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchAsync("test-topic", new RawPayload(ValidJson), CancellationToken.None));

            Assert.Equal("boom", exception.Message);
            Assert.Equal(new[] { "global-before", "handler", "global-error", "global-finally" }, events);
        }

        [Fact]
        public async Task DispatchAsync_Should_Run_Execution_Interceptor_Around_HubConsumer_Method()
        {
            var consumingConfigurator = new ConsumingConfigurator();
            var serializer = Substitute.For<ISerializer>();
            var events = new List<string>();

            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new TestMessage());

            var services = new ServiceCollection();
            services.AddSingleton<IConsumingConfigurator>(consumingConfigurator);
            services.AddSingleton(serializer);
            services.AddSingleton(events);
            services.AddScoped<ExecutionHubConsumer>();
            services.AddScoped<IConsumeExecutionInterceptor>(_ => new RecordingExecutionInterceptor(events, "global"));
            services.AddSingleton(new PigeonRouteInterceptorRegistry());
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();

            new ConsumerScanner(services, consumingConfigurator)
                .ScanHubConsumers(new[] { typeof(ExecutionHubConsumer) });

            var dispatcher = new ConsumingDispatcher(services.BuildServiceProvider());

            await dispatcher.DispatchAsync("hub-topic", new RawPayload(ValidJson), CancellationToken.None);

            Assert.Equal(new[] { "global-before", "hub-handler", "global-after", "global-finally" }, events);
        }

        [Fact]
        public async Task PigeonConsumerInvoker_Should_Replay_Envelope_Through_Handler_And_Accessor()
        {
            var consumingConfigurator = new ConsumingConfigurator();
            var serializer = new TestSerializer();
            ConsumeContext capturedContext = null;
            string capturedTenant = null;
            var handled = false;

            consumingConfigurator.AddConsumer<TestMessage>(
                "test-topic",
                SemanticVersion.Default,
                "billing",
                (context, message) =>
                {
                    handled = true;
                    capturedContext = context.Services.GetRequiredService<IConsumeContextAccessor>().ConsumeContext;
                    capturedTenant = context.GetMetadata<string>("tenant");
                    return Task.CompletedTask;
                });

            var services = new ServiceCollection();
            services.AddSingleton<IConsumingConfigurator>(consumingConfigurator);
            services.AddSingleton<ISerializer>(serializer);
            services.AddSingleton<ConsumeContextAccessor>();
            services.AddSingleton<IConsumeContextAccessor>(provider => provider.GetRequiredService<ConsumeContextAccessor>());
            services.AddSingleton<PigeonRouteInterceptorRegistry>();
            services.AddSingleton<IPigeonConsumeEnvelopeFactory, PigeonConsumeEnvelopeFactory>();
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();
            services.AddSingleton<IPigeonConsumerInvoker, PigeonConsumerInvoker>();

            var provider = services.BuildServiceProvider();
            var envelope = new PigeonConsumeEnvelope
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default,
                Subscription = "billing",
                PayloadType = typeof(TestMessage).AssemblyQualifiedName,
                Payload = System.Text.Encoding.UTF8.GetBytes(serializer.Serialize(new TestMessage())),
                CreatedOnUtc = DateTimeOffset.UtcNow,
                CorrelationId = "corr-1",
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tenant"] = @"""acme"""
                }
            };

            await provider.GetRequiredService<IPigeonConsumerInvoker>().InvokeAsync(envelope);

            Assert.True(handled);
            Assert.NotNull(capturedContext);
            Assert.Equal(ConsumeExecutionSource.DeferredReplay, capturedContext.ExecutionSource);
            Assert.Equal("corr-1", capturedContext.CorrelationId);
            Assert.Equal("acme", capturedTenant);
            Assert.Null(provider.GetRequiredService<IConsumeContextAccessor>().ConsumeContext);
        }

        [Fact]
        public async Task PigeonConsumerInvoker_Should_Run_Execution_Interceptor_During_Replay()
        {
            var consumingConfigurator = new ConsumingConfigurator();
            var serializer = new TestSerializer();
            var events = new List<string>();

            consumingConfigurator.AddConsumer<TestMessage>(
                "test-topic",
                SemanticVersion.Default,
                "billing",
                (context, message) =>
                {
                    events.Add(context.ExecutionSource == ConsumeExecutionSource.DeferredReplay
                        ? "handler-replay"
                        : "handler-live");
                    return Task.CompletedTask;
                });

            var services = new ServiceCollection();
            services.AddSingleton<IConsumingConfigurator>(consumingConfigurator);
            services.AddSingleton<ISerializer>(serializer);
            services.AddSingleton(new PigeonRouteInterceptorRegistry());
            services.AddScoped<IConsumeExecutionInterceptor>(_ => new RecordingExecutionInterceptor(events, "global"));
            services.AddSingleton<IPigeonConsumeEnvelopeFactory, PigeonConsumeEnvelopeFactory>();
            services.AddSingleton<IConsumeHandlerPipeline, ConsumeHandlerPipeline>();
            services.AddSingleton<IPigeonConsumerInvoker, PigeonConsumerInvoker>();

            var provider = services.BuildServiceProvider();
            var envelope = new PigeonConsumeEnvelope
            {
                Topic = "test-topic",
                Version = SemanticVersion.Default,
                Subscription = "billing",
                PayloadType = typeof(TestMessage).AssemblyQualifiedName,
                Payload = System.Text.Encoding.UTF8.GetBytes(serializer.Serialize(new TestMessage())),
                CreatedOnUtc = DateTimeOffset.UtcNow
            };

            await provider.GetRequiredService<IPigeonConsumerInvoker>().InvokeAsync(envelope);

            Assert.Equal(
                new[] { "global-before", "handler-replay", "global-after", "global-finally" },
                events);
        }

        private class TestMessage { }

        private sealed class FixedDecisionInterceptor : IConsumeDecisionInterceptor
        {
            private readonly PigeonConsumeDecision _decision;

            public FixedDecisionInterceptor(PigeonConsumeDecision decision)
            {
                _decision = decision;
            }

            public ValueTask<PigeonConsumeDecisionResult> InterceptAsync(
                ConsumeContext context,
                CancellationToken cancellationToken = default)
                => ValueTask.FromResult(new PigeonConsumeDecisionResult(_decision));
        }

        private sealed class RouteAckInterceptor : IConsumeDecisionInterceptor
        {
            public ValueTask<PigeonConsumeDecisionResult> InterceptAsync(
                ConsumeContext context,
                CancellationToken cancellationToken = default)
                => ValueTask.FromResult(new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
        }

        private sealed class RecordingExecutionInterceptor : IConsumeExecutionInterceptor
        {
            private readonly IList<string> _events;
            private readonly string _name;

            public RecordingExecutionInterceptor(IList<string> events, string name)
            {
                _events = events;
                _name = name;
            }

            public async ValueTask InvokeAsync(
                ConsumeContext context,
                ConsumeExecutionDelegate next,
                CancellationToken cancellationToken = default)
            {
                _events.Add($"{_name}-before");

                try
                {
                    await next(context, cancellationToken);
                    _events.Add($"{_name}-after");
                }
                catch
                {
                    _events.Add($"{_name}-error");
                    throw;
                }
                finally
                {
                    _events.Add($"{_name}-finally");
                }
            }
        }

        private sealed class RouteRecordingExecutionInterceptor : IConsumeExecutionInterceptor
        {
            private readonly List<string> _events;

            public RouteRecordingExecutionInterceptor(List<string> events)
            {
                _events = events;
            }

            public async ValueTask InvokeAsync(
                ConsumeContext context,
                ConsumeExecutionDelegate next,
                CancellationToken cancellationToken = default)
            {
                _events.Add("route-before");
                try
                {
                    await next(context, cancellationToken);
                    _events.Add("route-after");
                }
                finally
                {
                    _events.Add("route-finally");
                }
            }
        }

        private sealed class ExecutionHubConsumer : HubConsumer
        {
            private readonly List<string> _events;

            public ExecutionHubConsumer(List<string> events)
            {
                _events = events;
            }

            [Consumer("hub-topic", "1.0.0")]
            public Task Handle(TestMessage message, CancellationToken cancellationToken = default)
            {
                _events.Add("hub-handler");
                return Task.CompletedTask;
            }
        }

        private sealed class TestSerializer : ISerializer
        {
            public string Serialize(object payload)
                => System.Text.Json.JsonSerializer.Serialize(payload);

            public object Deserialize(string rawJson, Type targetType)
                => System.Text.Json.JsonSerializer.Deserialize(rawJson, targetType);
        }

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
