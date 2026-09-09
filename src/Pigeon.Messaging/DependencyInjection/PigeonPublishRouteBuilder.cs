namespace Microsoft.Extensions.DependencyInjection
{
    using Pigeon.Messaging;
    using Pigeon.Messaging.Producing;

    /// <summary>
    /// Configures services attached to a specific publish route.
    /// </summary>
    public sealed class PigeonPublishRouteBuilder
    {
        private readonly GlobalSettingsBuilder _builder;
        private readonly PigeonRouteKey _route;

        internal PigeonPublishRouteBuilder(GlobalSettingsBuilder builder, PigeonRouteKey route)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _route = route;
        }

        /// <summary>
        /// Registers a publish decision interceptor for this route.
        /// </summary>
        /// <typeparam name="TInterceptor">The interceptor type.</typeparam>
        /// <returns>The same route builder for chaining.</returns>
        public PigeonPublishRouteBuilder AddPublishDecisionInterceptor<TInterceptor>()
            where TInterceptor : class, IPublishDecisionInterceptor
        {
            _builder.AddRoutePublishDecisionInterceptor<TInterceptor>(_route);
            return this;
        }
    }
}
