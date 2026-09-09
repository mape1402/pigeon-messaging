namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;

    internal sealed class PigeonConsumerInvoker : IPigeonConsumerInvoker
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPigeonConsumeEnvelopeFactory _envelopeFactory;
        private readonly PigeonRouteInterceptorRegistry _routeInterceptorRegistry;

        public PigeonConsumerInvoker(
            IServiceProvider serviceProvider,
            IPigeonConsumeEnvelopeFactory envelopeFactory,
            PigeonRouteInterceptorRegistry routeInterceptorRegistry)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _envelopeFactory = envelopeFactory ?? throw new ArgumentNullException(nameof(envelopeFactory));
            _routeInterceptorRegistry = routeInterceptorRegistry ?? throw new ArgumentNullException(nameof(routeInterceptorRegistry));
        }

        public async ValueTask InvokeAsync(PigeonConsumeEnvelope envelope, CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = _envelopeFactory.CreateContext(envelope, scope.ServiceProvider, cancellationToken);
            var contextAccessor = scope.ServiceProvider.GetService<ConsumeContextAccessor>();

            using (contextAccessor?.Push(context))
            {
                foreach (var interceptor in scope.ServiceProvider.GetServices<IConsumeInterceptor>())
                    await interceptor.Intercept(context, cancellationToken);

                var route = new PigeonRouteKey(context.Topic, context.MessageVersion, context.Subscription);
                foreach (var interceptor in scope.ServiceProvider.GetServices<IConsumeDecisionInterceptor>())
                {
                    var decision = await interceptor.InterceptAsync(context, cancellationToken)
                        ?? PigeonConsumeDecisionResult.Continue;

                    if (decision.Decision != PigeonConsumeDecision.Continue)
                        return;
                }

                foreach (var interceptorType in _routeInterceptorRegistry.GetConsumeDecisionInterceptors(route))
                {
                    var interceptor = (IConsumeDecisionInterceptor)scope.ServiceProvider.GetRequiredService(interceptorType);
                    var decision = await interceptor.InterceptAsync(context, cancellationToken)
                        ?? PigeonConsumeDecisionResult.Continue;

                    if (decision.Decision != PigeonConsumeDecision.Continue)
                        return;
                }

                var consumingConfigurator = scope.ServiceProvider.GetRequiredService<IConsumingConfigurator>();
                var configuration = consumingConfigurator.GetConfiguration(context.Topic, context.MessageVersion, context.Subscription);
                configuration ??= consumingConfigurator.GetConfiguration(context.Topic, context.MessageVersion);

                if (configuration == null)
                    throw new InvalidOperationException($"No consumer configuration found for topic '{context.Topic}', version '{context.MessageVersion}', subscription '{context.Subscription}'.");

                await configuration.Handler(context);
            }
        }
    }
}
