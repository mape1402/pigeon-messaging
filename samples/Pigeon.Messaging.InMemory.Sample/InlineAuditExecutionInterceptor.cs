namespace Pigeon.Messaging.InMemory.Sample
{
    using Pigeon.Messaging.Consuming.Dispatching;

    internal sealed class InlineAuditExecutionInterceptor : IConsumeExecutionInterceptor
    {
        private readonly InMemorySampleScenario _scenario;

        public InlineAuditExecutionInterceptor(InMemorySampleScenario scenario)
        {
            _scenario = scenario;
        }

        public async ValueTask InvokeAsync(
            ConsumeContext context,
            ConsumeExecutionDelegate next,
            CancellationToken cancellationToken = default)
        {
            _scenario.RecordExecution("audit-before-handler");

            try
            {
                await next(context, cancellationToken);
                _scenario.RecordExecution("audit-after-handler");
            }
            finally
            {
                _scenario.RecordExecution("audit-finally");
            }
        }
    }
}
