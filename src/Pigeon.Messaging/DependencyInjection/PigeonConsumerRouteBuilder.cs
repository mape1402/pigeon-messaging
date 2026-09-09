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
