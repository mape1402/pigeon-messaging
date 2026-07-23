namespace Pigeon.Messaging.Rabbit.Sample
{
    using Pigeon.Messaging.Consuming.Management;

    internal sealed class RabbitSampleScenario
    {
        private readonly TaskCompletionSource _billingReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _auditReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public RabbitSampleScenario(
            string runId,
            string routingKey,
            string billingQueue,
            string auditQueue,
            int timeoutSeconds,
            MessageAcknowledgementMode acknowledgementMode,
            bool useOutbox,
            string outboxDatabasePath)
        {
            RunId = runId;
            RoutingKey = routingKey;
            BillingQueue = billingQueue;
            AuditQueue = auditQueue;
            TimeoutSeconds = timeoutSeconds;
            AcknowledgementMode = acknowledgementMode;
            UseOutbox = useOutbox;
            OutboxDatabasePath = outboxDatabasePath;
        }

        public string RunId { get; }

        public string RoutingKey { get; }

        public string BillingQueue { get; }

        public string AuditQueue { get; }

        public int TimeoutSeconds { get; }

        public MessageAcknowledgementMode AcknowledgementMode { get; }

        public bool UseOutbox { get; }

        public string OutboxDatabasePath { get; }

        public void MarkBilling(string orderId, string subscription)
        {
            if (orderId == RunId && subscription == BillingQueue)
                _billingReceived.TrySetResult();
        }

        public void MarkAudit(string orderId, string subscription)
        {
            if (orderId == RunId && subscription == AuditQueue)
                _auditReceived.TrySetResult();
        }

        public Task WaitForBothConsumersAsync(CancellationToken cancellationToken)
            => Task.WhenAll(
                _billingReceived.Task.WaitAsync(cancellationToken),
                _auditReceived.Task.WaitAsync(cancellationToken));
    }
}
