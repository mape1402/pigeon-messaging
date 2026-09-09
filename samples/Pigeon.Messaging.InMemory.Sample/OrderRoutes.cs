namespace Pigeon.Messaging.InMemory.Sample
{
    using Pigeon.Messaging;

    internal static class OrderRoutes
    {
        public const string CreatedTopic = "orders.created";
        public const string CreatedVersion = "1.0.0";
        public const string BillingSubscription = "billing-module";
        public const string AuditSubscription = "audit-module";

        public static readonly PigeonRouteKey CreatedForBilling =
            new(CreatedTopic, CreatedVersion, BillingSubscription);

        public static readonly PigeonRouteKey CreatedForAudit =
            new(CreatedTopic, CreatedVersion, AuditSubscription);
    }
}
