namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;

    /// <summary>
    /// Stores route-specific interceptor registrations used by Pigeon runtime builders.
    /// </summary>
    public sealed class PigeonRouteInterceptorRegistry
    {
        private readonly List<RouteInterceptorRegistration> _consumeDecisionInterceptors = new();
        private readonly List<RouteInterceptorRegistration> _publishDecisionInterceptors = new();

        /// <summary>
        /// Registers a consume decision interceptor for a route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <param name="interceptorType">The interceptor implementation type.</param>
        public void AddConsumeDecisionInterceptor(PigeonRouteKey route, Type interceptorType)
            => _consumeDecisionInterceptors.Add(new RouteInterceptorRegistration(Normalize(route), interceptorType));

        /// <summary>
        /// Registers a publish decision interceptor for a route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <param name="interceptorType">The interceptor implementation type.</param>
        public void AddPublishDecisionInterceptor(PigeonRouteKey route, Type interceptorType)
            => _publishDecisionInterceptors.Add(new RouteInterceptorRegistration(Normalize(route), interceptorType));

        /// <summary>
        /// Gets consume decision interceptor types registered for the route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <returns>The matching interceptor implementation types.</returns>
        public IReadOnlyCollection<Type> GetConsumeDecisionInterceptors(PigeonRouteKey route)
            => GetInterceptors(_consumeDecisionInterceptors, Normalize(route));

        /// <summary>
        /// Gets publish decision interceptor types registered for the route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <returns>The matching interceptor implementation types.</returns>
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
