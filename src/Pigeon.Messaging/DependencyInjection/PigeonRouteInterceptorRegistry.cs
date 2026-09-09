namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;

    internal sealed class PigeonRouteInterceptorRegistry
    {
        private readonly List<RouteInterceptorRegistration> _consumeDecisionInterceptors = new();
        private readonly List<RouteInterceptorRegistration> _publishDecisionInterceptors = new();

        public void AddConsumeDecisionInterceptor(PigeonRouteKey route, Type interceptorType)
            => _consumeDecisionInterceptors.Add(new RouteInterceptorRegistration(Normalize(route), interceptorType));

        public void AddPublishDecisionInterceptor(PigeonRouteKey route, Type interceptorType)
            => _publishDecisionInterceptors.Add(new RouteInterceptorRegistration(Normalize(route), interceptorType));

        public IReadOnlyCollection<Type> GetConsumeDecisionInterceptors(PigeonRouteKey route)
            => GetInterceptors(_consumeDecisionInterceptors, Normalize(route));

        public IReadOnlyCollection<Type> GetPublishDecisionInterceptors(PigeonRouteKey route)
            => GetInterceptors(_publishDecisionInterceptors, Normalize(route));

        private static IReadOnlyCollection<Type> GetInterceptors(
            IEnumerable<RouteInterceptorRegistration> registrations,
            PigeonRouteKey route)
            => registrations
                .Where(registration => registration.Route == route)
                .Select(registration => registration.InterceptorType)
                .ToArray();

        private static PigeonRouteKey Normalize(PigeonRouteKey route)
            => new(route.Topic, route.Version, string.IsNullOrWhiteSpace(route.Subscription)
                ? ConsumerEndpoint.DefaultSubscription
                : route.Subscription);

        private sealed record RouteInterceptorRegistration(PigeonRouteKey Route, Type InterceptorType);
    }
}
