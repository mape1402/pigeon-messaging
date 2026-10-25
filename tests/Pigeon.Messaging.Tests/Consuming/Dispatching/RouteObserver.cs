namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Pigeon.Messaging.Consuming.Dispatching;

    public sealed class RouteObserver : IConsumeDecisionObserver
    {
        private readonly Action<ConsumeDecisionObservation> _action;
        public RouteObserver() : this(_ => { }) { }
        public RouteObserver(Action<ConsumeDecisionObservation> action) => _action = action;
        public ValueTask ObserveAsync(ConsumeDecisionObservation observation, CancellationToken cancellationToken = default)
        {
            _action(observation);
            return ValueTask.CompletedTask;
        }
    }
}
