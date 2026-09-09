namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;
    using Pigeon.Messaging.Consuming.Configuration;

    internal sealed class ConsumeHandlerPipeline : IConsumeHandlerPipeline
    {
        private readonly PigeonRouteInterceptorRegistry _routeInterceptorRegistry;

        public ConsumeHandlerPipeline(PigeonRouteInterceptorRegistry routeInterceptorRegistry)
        {
            _routeInterceptorRegistry = routeInterceptorRegistry ?? throw new ArgumentNullException(nameof(routeInterceptorRegistry));
        }

        public async ValueTask InvokeAsync(
            ConsumeContext context,
            ConsumerConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            var route = new PigeonRouteKey(context.Topic, context.MessageVersion, context.Subscription);
            var interceptors = new List<IConsumeExecutionInterceptor>();

            interceptors.AddRange(context.Services.GetServices<IConsumeExecutionInterceptor>());

            foreach (var interceptorType in _routeInterceptorRegistry.GetConsumeExecutionInterceptors(route))
                interceptors.Add((IConsumeExecutionInterceptor)context.Services.GetRequiredService(interceptorType));

            ConsumeExecutionDelegate next = (ctx, token) =>
                new ValueTask(configuration.Handler(ctx));

            for (var index = interceptors.Count - 1; index >= 0; index--)
            {
                var interceptor = interceptors[index];
                var currentNext = next;
                next = (ctx, token) => interceptor.InvokeAsync(ctx, currentNext, token);
            }

            await next(context, cancellationToken);
        }
    }
}
