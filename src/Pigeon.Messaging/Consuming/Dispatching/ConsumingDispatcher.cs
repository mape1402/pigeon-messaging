namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Contracts;

    internal class ConsumingDispatcher : IConsumingDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly PigeonRouteInterceptorRegistry _routeInterceptorRegistry;

        public ConsumingDispatcher(IServiceProvider serviceProvider, PigeonRouteInterceptorRegistry routeInterceptorRegistry = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _routeInterceptorRegistry = routeInterceptorRegistry;
        }

        public async Task DispatchAsync(string topic, RawPayload rawPayload, CancellationToken cancellationToken = default)
            => await DispatchAsync(topic, Configuration.ConsumerEndpoint.DefaultSubscription, rawPayload, cancellationToken);

        public async Task DispatchAsync(string topic, string subscription, RawPayload rawPayload, CancellationToken cancellationToken = default)
            => await DispatchAsync(topic, subscription, rawPayload, null, null, cancellationToken);

        public async Task DispatchAsync(
            string topic,
            string subscription,
            RawPayload rawPayload,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync,
            CancellationToken cancellationToken = default)
            => await DispatchAsync(
                topic,
                subscription,
                rawPayload,
                completeAsync,
                failAsync,
                failAsync,
                failAsync,
                ConsumeExecutionSource.BrokerDelivery,
                cancellationToken);

        internal async Task DispatchAsync(
            string topic,
            string subscription,
            RawPayload rawPayload,
            Func<CancellationToken, Task> completeAsync,
            Func<Exception, CancellationToken, Task> failAsync,
            Func<Exception, CancellationToken, Task> retryAsync,
            Func<Exception, CancellationToken, Task> rejectAsync,
            ConsumeExecutionSource executionSource,
            CancellationToken cancellationToken = default)
        {
            if(string.IsNullOrWhiteSpace(topic))
                throw new ArgumentNullException(nameof(topic));

            using (var scope = _serviceProvider.CreateScope())
            {
                var consumingConfigurator = scope.ServiceProvider.GetRequiredService<IConsumingConfigurator>();
                var configuration = consumingConfigurator.GetConfiguration(topic, rawPayload.MessageVersion, subscription);
                configuration ??= consumingConfigurator.GetConfiguration(topic, rawPayload.MessageVersion);

                var interceptors = scope.ServiceProvider.GetServices<IConsumeInterceptor>();
                var serializer = scope.ServiceProvider.GetRequiredService<ISerializer>();

                var context = new ConsumeContext
                {
                    CancellationToken = cancellationToken,
                    CreatedOnUtc = rawPayload.CreatedOnUtc,
                    From = rawPayload.Domain,
                    MessageType = configuration.MessageType,
                    MessageVersion = configuration.Version,
                    Services = scope.ServiceProvider,
                    Subscription = configuration.Subscription,
                    Topic = topic,
                    Message = rawPayload.GetMessage(configuration.MessageType, serializer),
                    RawMetadata = rawPayload.GetMetadata(),
                    ExecutionSource = executionSource
                };
                context.SetAcknowledgementCallbacks(completeAsync, failAsync, retryAsync, rejectAsync);

                var contextAccessor = scope.ServiceProvider.GetService<ConsumeContextAccessor>();

                using (contextAccessor?.Push(context))
                {
                    foreach (var interceptor in interceptors)
                        await interceptor.Intercept(context, cancellationToken);

                    var decision = await GetConsumeDecisionAsync(
                        context,
                        new PigeonRouteKey(topic, configuration.Version, configuration.Subscription),
                        cancellationToken);

                    if (decision.Decision != PigeonConsumeDecision.Continue)
                    {
                        await ApplyDecisionAsync(context, decision, cancellationToken);
                        return;
                    }

                    await configuration.Handler(context);
                }
            }
        }

        private async ValueTask<PigeonConsumeDecisionResult> GetConsumeDecisionAsync(
            ConsumeContext context,
            PigeonRouteKey route,
            CancellationToken cancellationToken)
        {
            var globalInterceptors = context.Services.GetServices<IConsumeDecisionInterceptor>();

            foreach (var interceptor in globalInterceptors)
            {
                var decision = await interceptor.InterceptAsync(context, cancellationToken)
                    ?? PigeonConsumeDecisionResult.Continue;

                if (decision.Decision != PigeonConsumeDecision.Continue)
                    return decision;
            }

            var registry = _routeInterceptorRegistry ?? context.Services.GetService<PigeonRouteInterceptorRegistry>();
            if (registry == null)
                return PigeonConsumeDecisionResult.Continue;

            foreach (var interceptorType in registry.GetConsumeDecisionInterceptors(route))
            {
                var interceptor = (IConsumeDecisionInterceptor)context.Services.GetRequiredService(interceptorType);
                var decision = await interceptor.InterceptAsync(context, cancellationToken)
                    ?? PigeonConsumeDecisionResult.Continue;

                if (decision.Decision != PigeonConsumeDecision.Continue)
                    return decision;
            }

            return PigeonConsumeDecisionResult.Continue;
        }

        private static async Task ApplyDecisionAsync(
            ConsumeContext context,
            PigeonConsumeDecisionResult result,
            CancellationToken cancellationToken)
        {
            var exception = string.IsNullOrWhiteSpace(result.Reason)
                ? null
                : new InvalidOperationException(result.Reason);

            switch (result.Decision)
            {
                case PigeonConsumeDecision.AckAndSkip:
                case PigeonConsumeDecision.Defer:
                    await context.CompleteAsync(cancellationToken);
                    return;
                case PigeonConsumeDecision.Retry:
                    await context.RetryAsync(exception, cancellationToken);
                    return;
                case PigeonConsumeDecision.Reject:
                    await context.RejectAsync(exception, cancellationToken);
                    return;
                case PigeonConsumeDecision.Continue:
                default:
                    return;
            }
        }
    }
}
