namespace Pigeon.Testing.Tests
{
    using Pigeon.Messaging.Consuming.Dispatching;

    public sealed class CustomerCreatedConsumer : HubConsumer
    {
        private readonly CustomerProbe _probe;

        public CustomerCreatedConsumer(CustomerProbe probe)
        {
            _probe = probe;
        }

        [Consumer(nameof(CustomerCreatedMessage))]
        public Task Handle(CustomerCreatedMessage message, CancellationToken cancellationToken = default)
        {
            _probe.CustomerIds.Add(message.CustomerId);
            _probe.ConsumeContextWasAvailable = Context?.Message is CustomerCreatedMessage;
            return Task.CompletedTask;
        }
    }
}
