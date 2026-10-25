namespace Pigeon.Messaging.Tests.Consuming.Dispatching
{
    using Pigeon.Messaging.Consuming.Management;

    internal sealed class ObserverTestAdapter : IMessageBrokerConsumingAdapter
    {
        public event EventHandler<MessageConsumedEventArgs> MessageConsumed { add { } remove { } }
        public event MessageConsumedAsyncHandler MessageConsumedAsync;

        public ValueTask StartConsumeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopConsumeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public async Task DeliverAsync(MessageConsumedEventArgs args) => await MessageConsumedAsync(this, args, CancellationToken.None);
    }
}
