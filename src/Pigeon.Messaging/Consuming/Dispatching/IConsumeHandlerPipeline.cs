namespace Pigeon.Messaging.Consuming.Dispatching
{
    using Pigeon.Messaging.Consuming.Configuration;

    internal interface IConsumeHandlerPipeline
    {
        ValueTask InvokeAsync(
            ConsumeContext context,
            ConsumerConfiguration configuration,
            CancellationToken cancellationToken = default);
    }
}
