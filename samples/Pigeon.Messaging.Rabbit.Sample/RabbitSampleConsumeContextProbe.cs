namespace Pigeon.Messaging.Rabbit.Sample
{
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class RabbitSampleConsumeContextProbe
    {
        private readonly IConsumeContextAccessor _consumeContextAccessor;

        public RabbitSampleConsumeContextProbe(IConsumeContextAccessor consumeContextAccessor)
        {
            _consumeContextAccessor = consumeContextAccessor;
        }

        public string GetCurrentSubscription()
        {
            var context = _consumeContextAccessor.ConsumeContext
                ?? throw new InvalidOperationException("No consume context is available outside the Pigeon consume pipeline.");

            return context.Subscription;
        }
    }
}
