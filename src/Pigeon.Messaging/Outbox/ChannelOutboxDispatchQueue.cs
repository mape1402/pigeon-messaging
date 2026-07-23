namespace Pigeon.Messaging.Outbox
{
    using Microsoft.Extensions.Options;
    using System.Threading.Channels;

    internal sealed class ChannelOutboxDispatchQueue : IOutboxDispatchQueue
    {
        private readonly Channel<Guid> _channel;

        public ChannelOutboxDispatchQueue(IOptions<GlobalSettings> settings)
        {
            var capacity = settings?.Value?.Outbox?.DispatchQueueCapacity ?? 0;

            _channel = capacity > 0
                ? Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = false
                })
                : Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });
        }

        public ValueTask EnqueueAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
            => _channel.Writer.WriteAsync(outboxMessageId, cancellationToken);

        public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
            => _channel.Reader.ReadAsync(cancellationToken);
    }
}
