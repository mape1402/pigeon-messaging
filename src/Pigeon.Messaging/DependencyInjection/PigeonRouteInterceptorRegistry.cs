namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Configuration;
    using Pigeon.Messaging.Consuming.Dispatching;

    /// <summary>
    /// Stores route-specific interceptor registrations used by Pigeon runtime builders.
    /// </summary>
    public sealed class PigeonRouteInterceptorRegistry
    {
        private readonly List<RouteInterceptorRegistration> _consumeDecisionInterceptors = new();
        private readonly List<(PigeonRouteKey Route, Type Type, ObserverFailureBehavior Behavior)> _consumeDecisionObservers = new();
        private readonly List<RouteInterceptorRegistration> _consumeExecutionInterceptors = new();
        private readonly List<RouteInterceptorRegistration> _publishDecisionInterceptors = new();

        /// <summary>
        /// Registers a consume decision interceptor for a route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <param name="interceptorType">The interceptor implementation type.</param>
        public void AddConsumeDecisionInterceptor(PigeonRouteKey route, Type interceptorType)
            => _consumeDecisionInterceptors.Add(new RouteInterceptorRegistration(Normalize(route), interceptorType));

        /// <summary>Registers a decision observer for a route.</summary>
        /// <param name="route">The route key.</param>
        /// <param name="observerType">The observer implementation type.</param>
        public void AddConsumeDecisionObserver(PigeonRouteKey route, Type observerType)
            => AddConsumeDecisionObserver(route, observerType, ObserverFailureBehavior.Fail);

        /// <summary>Registers a decision observer and its failure policy for a route.</summary>
        /// <param name="route">The route key.</param>
        /// <param name="observerType">The observer type.</param>
        /// <param name="failureBehavior">The observer failure policy.</param>
        public void AddConsumeDecisionObserver(PigeonRouteKey route, Type observerType, ObserverFailureBehavior failureBehavior)
        {
            if (failureBehavior != ObserverFailureBehavior.Fail && failureBehavior != ObserverFailureBehavior.Continue)
                throw new ArgumentOutOfRangeException(nameof(failureBehavior));
            _consumeDecisionObservers.Add((Normalize(route), observerType, failureBehavior));
        }

        /// <summary>
        /// Registers a consume execution interceptor for a route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <param name="interceptorType">The interceptor implementation type.</param>
        public void AddConsumeExecutionInterceptor(PigeonRouteKey route, Type interceptorType)
            => _consumeExecutionInterceptors.Add(new RouteInterceptorRegistration(Normalize(route), interceptorType));

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

        /// <summary>Gets decision observer types registered for a route.</summary>
        /// <param name="route">The route key.</param>
        /// <returns>The matching observer types in registration order.</returns>
        public IReadOnlyCollection<Type> GetConsumeDecisionObservers(PigeonRouteKey route)
            => GetConsumeDecisionObserverRegistrations(route).Select(item => item.Type).ToArray();

        internal IReadOnlyCollection<(Type Type, ObserverFailureBehavior Behavior)> GetConsumeDecisionObserverRegistrations(PigeonRouteKey route)
            => _consumeDecisionObservers.Where(item => item.Route == Normalize(route)).Select(item => (item.Type, item.Behavior)).ToArray();

        /// <summary>
        /// Gets consume execution interceptor types registered for the route.
        /// </summary>
        /// <param name="route">The route key.</param>
        /// <returns>The matching interceptor implementation types.</returns>
        public IReadOnlyCollection<Type> GetConsumeExecutionInterceptors(PigeonRouteKey route)
            => GetInterceptors(_consumeExecutionInterceptors, Normalize(route));

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
