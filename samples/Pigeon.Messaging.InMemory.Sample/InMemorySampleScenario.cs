namespace Pigeon.Messaging.InMemory.Sample
{
    internal sealed class InMemorySampleScenario
    {
        private readonly TaskCompletionSource _billingReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _auditReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string OrderId { get; } = Guid.NewGuid().ToString("N");

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

        public Task WaitForBothModulesAsync(CancellationToken cancellationToken)
            => Task.WhenAll(
                _billingReceived.Task.WaitAsync(cancellationToken),
                _auditReceived.Task.WaitAsync(cancellationToken));
    }
}
