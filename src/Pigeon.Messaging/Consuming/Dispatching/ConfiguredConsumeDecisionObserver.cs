namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Microsoft.Extensions.DependencyInjection;

    internal sealed class ConfiguredConsumeDecisionObserver : IConsumeDecisionObserver
    {
        private readonly IServiceProvider _services;
        internal Type ObserverType { get; }
        internal ObserverFailureBehavior Behavior { get; }

        internal ConfiguredConsumeDecisionObserver(IServiceProvider services, Type observerType, ObserverFailureBehavior behavior)
        {
            _services = services;
            ObserverType = observerType;
            Behavior = behavior;
        }

        public ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default)
            => ((IConsumeDecisionObserver)_services.GetRequiredService(ObserverType)).ObserveAsync(observation, cancellationToken);
    }
}
