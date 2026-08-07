namespace Pigeon.Testing.Tests
{
    using Pigeon.Messaging.Producing;

    public sealed class CorrelationPublishInterceptor : IPublishInterceptor
    {
        private readonly CorrelationIdHolder _holder;

        public CorrelationPublishInterceptor(CorrelationIdHolder holder)
        {
            _holder = holder;
        }

        public ValueTask Intercept(PublishContext publishContext, CancellationToken cancellationToken = default)
        {
            publishContext.AddMetadata("correlation-id", _holder.CorrelationId);
            return ValueTask.CompletedTask;
        }
    }
}
