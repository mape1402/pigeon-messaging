namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging;
    using Pigeon.Messaging.Consuming.Dispatching;

    /// <summary>
    /// Configures services attached to a specific consume route.
    /// </summary>
    public sealed class PigeonConsumerRouteBuilder
    {
        private readonly GlobalSettingsBuilder _builder;
        private readonly PigeonRouteKey _route;

        internal PigeonConsumerRouteBuilder(GlobalSettingsBuilder builder, PigeonRouteKey route)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _route = route;
        }

        /// <summary>
        /// Registers a consume decision interceptor for this route.
        /// </summary>
        /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
        /// <returns>The same route builder for chaining.</returns>
        public PigeonConsumerRouteBuilder AddConsumeDecisionInterceptor<TInterceptor>()
            where TInterceptor : class, IConsumeDecisionInterceptor
        {
            _builder.AddRouteConsumeDecisionInterceptor<TInterceptor>(_route);
            return this;
        }

        /// <summary>Registers a decision observer for this route.</summary>
        /// <typeparam name="TObserver">The observer type.</typeparam>
        /// <returns>The same route builder for chaining.</returns>
        public PigeonConsumerRouteBuilder AddConsumeDecisionObserver<TObserver>()
            where TObserver : class, IConsumeDecisionObserver
            => AddConsumeDecisionObserver<TObserver>(ObserverFailureBehavior.Fail);

        /// <summary>Registers a route observer with its failure policy.</summary>
        /// <typeparam name="TObserver">The observer type.</typeparam>
        /// <param name="failureBehavior">How an observer failure affects processing.</param>
        /// <returns>The same route builder for chaining.</returns>
        public PigeonConsumerRouteBuilder AddConsumeDecisionObserver<TObserver>(ObserverFailureBehavior failureBehavior)
            where TObserver : class, IConsumeDecisionObserver
        {
            _builder.AddRouteConsumeDecisionObserver<TObserver>(_route, failureBehavior);
            return this;
        }

        /// <summary>
        /// Registers a consume execution interceptor for this route.
        /// </summary>
        /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
        /// <returns>The same route builder for chaining.</returns>
        public PigeonConsumerRouteBuilder AddConsumeExecutionInterceptor<TInterceptor>()
            where TInterceptor : class, IConsumeExecutionInterceptor
        {
            _builder.AddRouteConsumeExecutionInterceptor<TInterceptor>(_route);
            return this;
        }
    }
}
