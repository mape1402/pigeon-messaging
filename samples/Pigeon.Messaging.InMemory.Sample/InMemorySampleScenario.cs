namespace Pigeon.Messaging.InMemory.Sample
{
    using System.Collections.Concurrent;
    using Pigeon.Messaging.Consuming.Dispatching;
    using Pigeon.Messaging.Producing;

    internal sealed class InMemorySampleScenario
    {
        private readonly TaskCompletionSource _billingReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _auditReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _externalOutboxReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<PigeonConsumeEnvelope> _deferredEnvelope = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<PigeonPublishEnvelope> _externalOutboxEnvelope = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<string> _executionEvents = new();

        public string OrderId { get; } = Guid.NewGuid().ToString("N");

        public IReadOnlyCollection<string> ExecutionEvents
            => _executionEvents.ToArray();

        public void MarkBilling(string orderId)
        {
            if (orderId == OrderId)
                _billingReceived.TrySetResult();
        }

        public void MarkAudit(string orderId)
        {
            if (orderId == OrderId)
                _auditReceived.TrySetResult();
        }

        public void MarkExternalOutbox(string orderId)
        {
            if (orderId == OrderId)
                _externalOutboxReceived.TrySetResult();
        }

        public void CaptureDeferredEnvelope(PigeonConsumeEnvelope envelope)
            => _deferredEnvelope.TrySetResult(envelope);

        public void CaptureExternalOutboxEnvelope(PigeonPublishEnvelope envelope)
            => _externalOutboxEnvelope.TrySetResult(envelope);

        public void RecordExecution(string eventName)
            => _executionEvents.Enqueue(eventName);

        public Task WaitForBothModulesAsync(CancellationToken cancellationToken)
            => Task.WhenAll(
                _billingReceived.Task.WaitAsync(cancellationToken),
                _auditReceived.Task.WaitAsync(cancellationToken));

        public Task WaitForAuditAsync(CancellationToken cancellationToken)
            => _auditReceived.Task.WaitAsync(cancellationToken);

        public Task WaitForExternalOutboxAsync(CancellationToken cancellationToken)
            => _externalOutboxReceived.Task.WaitAsync(cancellationToken);

        public Task<PigeonConsumeEnvelope> WaitForDeferredEnvelopeAsync(CancellationToken cancellationToken)
            => _deferredEnvelope.Task.WaitAsync(cancellationToken);

        public Task<PigeonPublishEnvelope> WaitForExternalOutboxEnvelopeAsync(CancellationToken cancellationToken)
            => _externalOutboxEnvelope.Task.WaitAsync(cancellationToken);
    }
}
