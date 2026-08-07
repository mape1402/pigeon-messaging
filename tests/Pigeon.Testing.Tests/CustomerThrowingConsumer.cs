namespace Pigeon.Testing.Tests
{
    using Pigeon.Messaging.Consuming.Dispatching;

    public sealed class CustomerThrowingConsumer : HubConsumer
    {
        [Consumer(nameof(CustomerThrowingMessage))]
        public Task Handle(CustomerThrowingMessage message, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("consumer failed");
    }
}
