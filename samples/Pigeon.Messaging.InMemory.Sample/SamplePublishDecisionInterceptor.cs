namespace Pigeon.Messaging.InMemory.Sample
{
    using Pigeon.Messaging.Producing;

    internal sealed class SamplePublishDecisionInterceptor : IPublishDecisionInterceptor
    {
        public ValueTask<PigeonPublishDecisionResult> InterceptAsync(
            PublishContext context,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new PigeonPublishDecisionResult(PigeonPublishDecision.Continue)
            {
                Metadata = new Dictionary<string, object>
                {
                    ["correlation-id"] = Guid.NewGuid().ToString("N")
                }
            });
    }
}
