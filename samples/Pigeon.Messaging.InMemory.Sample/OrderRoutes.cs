namespace Pigeon.Messaging.InMemory.Sample
{
    using Pigeon.Messaging;

    internal static class OrderRoutes
    {
        public const string CreatedTopic = "orders.created";
        public const string ExternalOutboxTopic = "orders.external-outbox";
        public const string CreatedVersion = "1.0.0";
        public const string BillingSubscription = "billing-module";
        public const string AuditSubscription = "audit-module";
        public const string ExternalOutboxSubscription = "external-outbox-module";

        public static readonly PigeonRouteKey CreatedForBilling =
            new(CreatedTopic, CreatedVersion, BillingSubscription);

        public static readonly PigeonRouteKey CreatedForAudit =
            new(CreatedTopic, CreatedVersion, AuditSubscription);

        public static readonly PigeonRouteKey ExternalOutbox =
            new(ExternalOutboxTopic, CreatedVersion, ExternalOutboxSubscription);
    }
}
