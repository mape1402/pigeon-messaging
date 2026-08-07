namespace Pigeon.Testing
{
    using Pigeon.Messaging.Consuming.Management;

    internal sealed class PigeonTestingConsumingAdapter : IMessageBrokerConsumingAdapter
    {
        event EventHandler<MessageConsumedEventArgs> IMessageBrokerConsumingAdapter.MessageConsumed
        {
            add { }
            remove { }
        }

        public ValueTask StartConsumeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask StopConsumeAsync(CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
