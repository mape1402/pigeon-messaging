namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Consuming;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Consuming.Management;
    using Pigeon.Messaging.Contracts;
    using System.Diagnostics.Metrics;

    public class ConsumeDecisionObserverTests
    {
        private const string Payload = "{\"Domain\":\"test\",\"MessageVersion\":\"1.0.0\",\"CreatedOnUtc\":\"2024-01-01T00:00:00Z\",\"Message\":{},\"Metadata\":{}}";
        private static readonly PigeonRouteKey Route = new("orders", SemanticVersion.Default, "billing");

        [Theory]
        [InlineData(PigeonConsumeDecision.AckAndSkip, "complete")]
        [InlineData(PigeonConsumeDecision.Defer, "complete")]
        [InlineData(PigeonConsumeDecision.Retry, "retry")]
        [InlineData(PigeonConsumeDecision.Reject, "reject")]
        public async Task TerminalDecision_NotifiesObserversInOrderBeforeSettlement(PigeonConsumeDecision decision, string settlement)
        {
            var events = new List<string>();
            var result = new PigeonConsumeDecisionResult(decision, "reason")
            {
                Outcome = new PigeonConsumeOutcome { Kind = PigeonConsumeOutcomeKind.AlreadyProcessed },
                Metadata = new Dictionary<string, string> { ["TRACE"] = "decision", ["extra"] = "value" }
            };
            var services = CreateServices(events, result);
            var first = Observer(observation =>
            {
                events.Add("global-1");
                Assert.Same(result, observation.Result);
                Assert.Equal("decision", observation.Context.ReplyMetadata["trace"]);
                Assert.Equal("value", observation.Context.ReplyMetadata["EXTRA"]);
                Assert.Equal(PigeonConsumeOutcomeKind.AlreadyProcessed, observation.Result.Outcome.Kind);
            });
            var metadataInterceptor = Substitute.For<IConsumeInterceptor>();
            metadataInterceptor.Intercept(Arg.Any<ConsumeContext>(), Arg.Any<CancellationToken>())
                .Returns(call => { call.Arg<ConsumeContext>().ReplyMetadata["trace"] = "existing"; return ValueTask.CompletedTask; });
            services.AddScoped(_ => metadataInterceptor);
            services.AddScoped<IConsumeDecisionObserver>(_ => first);
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("global-2")));
            services.AddScoped<RouteObserver>(_ => new RouteObserver(_ => events.Add("route")));
            var registry = new PigeonRouteInterceptorRegistry();
            registry.AddConsumeDecisionObserver(Route, typeof(RouteObserver));
            using var provider = services.BuildServiceProvider();

            await Dispatch(provider, registry, events);

            Assert.Equal(new[] { "global-1", "global-2", "route", settlement }, events);
        }

        [Fact]
        public async Task Continue_DoesNotNotifyObservers_AndInvokesHandler()
        {
            var events = new List<string>();
            var services = CreateServices(events, PigeonConsumeDecisionResult.Continue);
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("observer")));
            using var provider = services.BuildServiceProvider();

            await Dispatch(provider, new PigeonRouteInterceptorRegistry(), events);

            Assert.Equal(new[] { "handler" }, events);
        }

        [Theory]
        [InlineData(PigeonConsumeDecision.AckAndSkip, "complete")]
        [InlineData(PigeonConsumeDecision.Reject, "reject")]
        [InlineData(PigeonConsumeDecision.Retry, "retry")]
        [InlineData(PigeonConsumeDecision.Defer, "complete")]
        public async Task OptionalObserverFailures_ContinueInOrderAndApplyDecision(PigeonConsumeDecision decision, string settlement)
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(decision));
            services.AddScoped<RouteObserver>(_ => new RouteObserver(_ => { events.Add("optional"); throw new InvalidOperationException(); }));
            services.AddScoped<IConsumeDecisionObserver>(provider => new ConfiguredConsumeDecisionObserver(provider, typeof(RouteObserver), ObserverFailureBehavior.Continue));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("next")));
            var registry = new PigeonRouteInterceptorRegistry();
            registry.AddConsumeDecisionObserver(Route, typeof(RouteObserver), ObserverFailureBehavior.Continue);
            using var provider = services.BuildServiceProvider();

            await Dispatch(provider, registry, events);

            Assert.Equal(new[] { "optional", "next", "optional", settlement }, events);
        }

        [Fact]
        public async Task SameObserverType_CanUseDifferentPoliciesPerRegistration()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<RouteObserver>(_ => new RouteObserver(_ => { events.Add("observer"); throw new InvalidOperationException(); }));
            services.AddScoped<IConsumeDecisionObserver>(provider => new ConfiguredConsumeDecisionObserver(provider, typeof(RouteObserver), ObserverFailureBehavior.Continue));
            var registry = new PigeonRouteInterceptorRegistry();
            registry.AddConsumeDecisionObserver(Route, typeof(RouteObserver), ObserverFailureBehavior.Fail);
            using var provider = services.BuildServiceProvider();

            await Dispatch(provider, registry, events);

            Assert.Equal(new[] { "observer", "observer", "retry" }, events);
        }

        [Fact]
        public async Task OptionalObserver_DoesNotSuppressOperationCancellation()
        {
            var services = new ServiceCollection();
            services.AddScoped<RouteObserver>(_ => new RouteObserver(_ => throw new OperationCanceledException()));
            services.AddScoped<IConsumeDecisionObserver>(provider => new ConfiguredConsumeDecisionObserver(provider, typeof(RouteObserver), ObserverFailureBehavior.Continue));
            using var provider = services.BuildServiceProvider();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var context = new ConsumeContext { Services = provider, Topic = "orders", MessageVersion = SemanticVersion.Default, Subscription = "billing" };

            await Assert.ThrowsAsync<OperationCanceledException>(() => ConsumeDecisionPipeline.ObserveAsync(context,
                new PigeonConsumeDecisionResult(PigeonConsumeDecision.Reject), Route, null, cancellation.Token).AsTask());
        }

        [Fact]
        public async Task Observers_AreScopedPerMessageAndAwaitedBeforeSettlement()
        {
            var events = new List<string>();
            var scopes = new List<ObserverScopeMarker>();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<ObserverScopeMarker>();
            services.AddScoped<IConsumeDecisionObserver>(provider =>
            {
                var marker = provider.GetRequiredService<ObserverScopeMarker>();
                var observer = Substitute.For<IConsumeDecisionObserver>();
                async Task Observe(ConsumeDecisionObservation observation)
                {
                    Assert.Same(marker, observation.Context.Services.GetRequiredService<ObserverScopeMarker>());
                    scopes.Add(marker);
                    entered.TrySetResult();
                    await release.Task;
                }
                observer.ObserveAsync(Arg.Any<ConsumeDecisionObservation>(), Arg.Any<CancellationToken>())
                    .Returns(call => new ValueTask(Observe(call.Arg<ConsumeDecisionObservation>())));
                return observer;
            });
            using var provider = services.BuildServiceProvider();
            var first = Dispatch(provider, new PigeonRouteInterceptorRegistry(), events);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Empty(events);
            release.SetResult();
            await first;
            await Dispatch(provider, new PigeonRouteInterceptorRegistry(), events);

            Assert.Equal(new[] { "complete", "complete" }, events);
            Assert.Equal(2, scopes.Count);
            Assert.NotSame(scopes[0], scopes[1]);
        }

        [Theory]
        [InlineData(PigeonConsumeDecision.AckAndSkip)]
        [InlineData(PigeonConsumeDecision.Defer)]
        [InlineData(PigeonConsumeDecision.Retry)]
        [InlineData(PigeonConsumeDecision.Reject)]
        public async Task ObserverFailure_RequestsRetryAndStopsFurtherObservers(PigeonConsumeDecision decision)
        {
            var events = new List<string>();
            var expected = new InvalidOperationException("observer failed");
            var services = CreateServices(events, new PigeonConsumeDecisionResult(decision));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("first")));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw expected));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("last")));
            using var provider = services.BuildServiceProvider();
            Exception actual = null;

            await Dispatch(provider, new PigeonRouteInterceptorRegistry(), events, ex => actual = ex);

            Assert.Same(expected, actual);
            Assert.Equal(new[] { "first", "retry" }, events);
        }

        [Fact]
        public async Task RetryTransportFailure_PropagatesAndDoesNotSettleOriginalDecision()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.Reject));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();
            var failure = new TimeoutException("transport");

            var exception = await Assert.ThrowsAsync<ConsumeObserverRetryException>(() =>
                Dispatch(provider, new PigeonRouteInterceptorRegistry(), events, retryFailure: failure));

            Assert.Same(failure, exception.InnerException);
            Assert.IsType<InvalidOperationException>(exception.ObserverException);
            Assert.Equal(new[] { "retry" }, events);
        }

        [Fact]
        public async Task ObserverFailureWithoutRetryCallback_Propagates()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();
            var dispatcher = new ConsumingDispatcher(provider);

            var failure = await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => dispatcher.DispatchAsync("orders", "billing", new RawPayload(Payload)));
            Assert.IsType<InvalidOperationException>(failure.ObserverException);
            Assert.IsType<NotSupportedException>(failure.InnerException);
            Assert.Empty(events);
        }

        [Fact]
        public async Task NoObservers_PreservesTerminalSettlementAndLegacyOutcome()
        {
            var events = new List<string>();
            var result = new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip);
            using var provider = CreateServices(events, result).BuildServiceProvider();

            await Dispatch(provider, new PigeonRouteInterceptorRegistry(), events);

            Assert.Null(result.Outcome);
            Assert.Equal(new[] { "complete" }, events);
        }

        [Fact]
        public async Task Replay_ObservesMetadataAndPropagatesObserverFailure()
        {
            var events = new List<string>();
            var result = new PigeonConsumeDecisionResult(PigeonConsumeDecision.Defer)
            {
                Metadata = new Dictionary<string, string> { ["trace"] = "replay" }
            };
            var services = CreateServices(events, result);
            var failure = new InvalidOperationException("observer");
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(observation =>
            {
                Assert.Equal(ConsumeExecutionSource.DeferredReplay, observation.Context.ExecutionSource);
                Assert.Equal("replay", observation.Context.ReplyMetadata["TRACE"]);
                Assert.Same(result, observation.Result);
                throw failure;
            }));
            using var provider = services.BuildServiceProvider();
            var invoker = new PigeonConsumerInvoker(provider, new PigeonConsumeEnvelopeFactory(provider.GetRequiredService<ISerializer>()),
                new PigeonRouteInterceptorRegistry(), new ConsumeHandlerPipeline(new PigeonRouteInterceptorRegistry()));
            var envelope = new PigeonConsumeEnvelope
            {
                Topic = "orders", Version = SemanticVersion.Default, Subscription = "billing",
                Payload = System.Text.Encoding.UTF8.GetBytes("{}")
            };

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => invoker.InvokeAsync(envelope).AsTask());
            Assert.Same(failure, exception);
            Assert.Empty(events);
        }

        [Fact]
        public async Task Replay_ExecutesGlobalAndRouteObserversInOrder()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.Reject));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("global")));
            services.AddScoped<RouteObserver>(_ => new RouteObserver(_ => events.Add("route")));
            var registry = new PigeonRouteInterceptorRegistry();
            registry.AddConsumeDecisionObserver(Route, typeof(RouteObserver));
            using var provider = services.BuildServiceProvider();
            var invoker = new PigeonConsumerInvoker(provider, new PigeonConsumeEnvelopeFactory(provider.GetRequiredService<ISerializer>()),
                registry, new ConsumeHandlerPipeline(registry));

            await invoker.InvokeAsync(new PigeonConsumeEnvelope
            {
                Topic = "orders", Version = SemanticVersion.Default, Subscription = "billing",
                Payload = System.Text.Encoding.UTF8.GetBytes("{}")
            });

            Assert.Equal(new[] { "global", "route" }, events);
        }

        [Fact]
        public async Task Replay_ContinuesAfterOptionalObserverFailure()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.Defer));
            services.AddScoped<RouteObserver>(_ => new RouteObserver(_ => throw new InvalidOperationException("optional")));
            services.AddScoped<IConsumeDecisionObserver>(provider => new ConfiguredConsumeDecisionObserver(provider, typeof(RouteObserver), ObserverFailureBehavior.Continue));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => events.Add("next")));
            using var provider = services.BuildServiceProvider();
            var registry = new PigeonRouteInterceptorRegistry();
            var invoker = new PigeonConsumerInvoker(provider, new PigeonConsumeEnvelopeFactory(provider.GetRequiredService<ISerializer>()),
                registry, new ConsumeHandlerPipeline(registry));

            await invoker.InvokeAsync(new PigeonConsumeEnvelope
            {
                Topic = "orders", Version = SemanticVersion.Default, Subscription = "billing",
                Payload = System.Text.Encoding.UTF8.GetBytes("{}")
            });

            Assert.Equal(new[] { "next" }, events);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Manager_DoesNotAcknowledgeAfterObserverRetry(bool retryFails)
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();
            var adapter = new ObserverTestAdapter();
            var settings = Options.Create(new GlobalSettings
            {
                ConsumerExecution = new ConsumerExecutionSettings { AcknowledgementMode = MessageAcknowledgementMode.OnHandlerSuccess }
            });
            var manager = new ConsumingManager(new ConsumingDispatcher(provider), new[] { adapter }, settings, NullLogger<ConsumingManager>.Instance);
            await manager.StartAsync();
            var args = new MessageConsumedEventArgs("orders", Payload, "billing",
                _ => { events.Add("complete"); return Task.CompletedTask; },
                (_, _) => Task.CompletedTask,
                (_, _) => { events.Add("retry"); return retryFails ? Task.FromException(new TimeoutException()) : Task.CompletedTask; },
                (_, _) => { events.Add("reject"); return Task.CompletedTask; });

            if (retryFails)
                await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => adapter.DeliverAsync(args));
            else
                await adapter.DeliverAsync(args);

            await manager.StopAsync();
            Assert.Equal(new[] { "retry" }, events);
        }

        [Fact]
        public async Task Manager_PropagatesWhenTransportHasNoRetryCallback()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();
            var adapter = new ObserverTestAdapter();
            var manager = new ConsumingManager(new ConsumingDispatcher(provider), new[] { adapter },
                Options.Create(new GlobalSettings()), NullLogger<ConsumingManager>.Instance);
            await manager.StartAsync();

            var failure = await Assert.ThrowsAsync<ConsumeObserverRetryException>(() =>
                adapter.DeliverAsync(new MessageConsumedEventArgs("orders", Payload, "billing")));

            Assert.IsType<NotSupportedException>(failure.InnerException);
            await manager.StopAsync();
            Assert.Empty(events);
        }

        [Fact]
        public async Task Manager_PropagatesForSynchronousTransportEvenWhenQueueIsConfigured()
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.AckAndSkip));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();
            var adapter = new ObserverTestAdapter();
            var manager = new ConsumingManager(new ConsumingDispatcher(provider), new[] { adapter },
                Options.Create(new GlobalSettings { ConsumerExecution = new ConsumerExecutionSettings { MaxConcurrency = 1 } }),
                NullLogger<ConsumingManager>.Instance);
            await manager.StartAsync();
            try
            {
                var args = new MessageConsumedEventArgs("orders", Payload, "billing", null, null, null, null,
                    supportsRetry: false, requiresSynchronousDispatch: true);
                await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => adapter.DeliverAsync(args));
            }
            finally
            {
                await manager.StopAsync();
            }
            Assert.Empty(events);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Manager_RespectsDeclaredRetryCapabilityWithoutChangingExistingDecisions(bool observerFails)
        {
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.Retry));
            if (observerFails)
                services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();
            var adapter = new ObserverTestAdapter();
            var manager = new ConsumingManager(new ConsumingDispatcher(provider), new[] { adapter },
                Options.Create(new GlobalSettings()), NullLogger<ConsumingManager>.Instance);
            await manager.StartAsync();
            var args = new MessageConsumedEventArgs("orders", Payload, "billing",
                _ => { events.Add("complete"); return Task.CompletedTask; },
                (_, _) => { events.Add("failure"); return Task.CompletedTask; },
                (_, _) => { events.Add("retry"); return Task.CompletedTask; },
                (_, _) => { events.Add("reject"); return Task.CompletedTask; }, supportsRetry: false);

            if (observerFails)
                await Assert.ThrowsAsync<ConsumeObserverRetryException>(() => adapter.DeliverAsync(args));
            else
                await adapter.DeliverAsync(args);

            await manager.StopAsync();
            Assert.Equal(observerFails ? Array.Empty<string>() : new[] { "retry" }, events);
        }

        [Fact]
        public async Task TerminalDecision_EmitsObserverCounters()
        {
            var counts = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == "Pigeon.Messaging.ConsumeDecisions")
                    meterListener.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => counts.AddOrUpdate(instrument.Name, value, (_, current) => current + value));
            listener.Start();
            var events = new List<string>();
            var services = CreateServices(events, new PigeonConsumeDecisionResult(PigeonConsumeDecision.Reject));
            services.AddScoped<IConsumeDecisionObserver>(_ => Observer(_ => throw new InvalidOperationException("observer")));
            using var provider = services.BuildServiceProvider();

            await Dispatch(provider, new PigeonRouteInterceptorRegistry(), events);

            Assert.True(counts["pigeon.consume.decisions.terminal"] >= 1);
            Assert.True(counts["pigeon.consume.observers.executions"] >= 1);
            Assert.True(counts["pigeon.consume.observers.failures"] >= 1);
            Assert.True(counts["pigeon.consume.observers.retries"] >= 1);
        }

        [Fact]
        public void Registration_UsesScopedServicesAndMatchesOnlyConfiguredRoute()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Pigeon:Domain"] = "test" }).Build();
            var pigeon = services.AddPigeon(configuration, _ => { });
            pigeon.AddConsumeDecisionObserver<RouteObserver>();
            pigeon.ForConsumer("orders", SemanticVersion.Default, "billing").AddConsumeDecisionObserver<RouteObserver>();

            Assert.Contains(services, d => d.ServiceType == typeof(IConsumeDecisionObserver) && d.ImplementationType == typeof(RouteObserver) && d.Lifetime == ServiceLifetime.Scoped);
            Assert.Contains(services, d => d.ServiceType == typeof(RouteObserver) && d.Lifetime == ServiceLifetime.Scoped);
            using var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<PigeonRouteInterceptorRegistry>();
            Assert.Equal(new[] { typeof(RouteObserver) }, registry.GetConsumeDecisionObservers(Route));
            Assert.Empty(registry.GetConsumeDecisionObservers(new PigeonRouteKey("orders", SemanticVersion.Default, "audit")));
        }

        private static ServiceCollection CreateServices(List<string> events, PigeonConsumeDecisionResult result)
        {
            var services = new ServiceCollection();
            var config = Substitute.For<IConsumingConfigurator>();
            var serializer = Substitute.For<ISerializer>();
            serializer.Deserialize(Arg.Any<string>(), Arg.Any<Type>()).Returns(new object());
            var consumer = new ConsumerConfiguration<object>((_, _) => { events.Add("handler"); return Task.CompletedTask; })
            { Topic = "orders", Version = SemanticVersion.Default, Subscription = "billing" };
            config.GetConfiguration("orders", SemanticVersion.Default, "billing").Returns(consumer);
            services.AddSingleton(config);
            services.AddSingleton(serializer);
            var interceptor = Substitute.For<IConsumeDecisionInterceptor>();
            interceptor.InterceptAsync(Arg.Any<ConsumeContext>(), Arg.Any<CancellationToken>()).Returns(new ValueTask<PigeonConsumeDecisionResult>(result));
            services.AddScoped(_ => interceptor);
            return services;
        }

        private static IConsumeDecisionObserver Observer(Action<ConsumeDecisionObservation> action)
        {
            var observer = Substitute.For<IConsumeDecisionObserver>();
            observer.ObserveAsync(Arg.Any<ConsumeDecisionObservation>(), Arg.Any<CancellationToken>())
                .Returns(call => { action(call.Arg<ConsumeDecisionObservation>()); return ValueTask.CompletedTask; });
            return observer;
        }

        private static Task Dispatch(ServiceProvider provider, PigeonRouteInterceptorRegistry registry, List<string> events,
            Action<Exception> onRetry = null, Exception retryFailure = null)
            => new ConsumingDispatcher(provider, registry).DispatchAsync("orders", "billing", new RawPayload(Payload),
                _ => { events.Add("complete"); return Task.CompletedTask; },
                (_, _) => Task.CompletedTask,
                (exception, _) => { events.Add("retry"); onRetry?.Invoke(exception); return retryFailure == null ? Task.CompletedTask : Task.FromException(retryFailure); },
                (_, _) => { events.Add("reject"); return Task.CompletedTask; },
                ConsumeExecutionSource.BrokerDelivery, CancellationToken.None);

    }
}
