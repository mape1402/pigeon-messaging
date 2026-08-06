namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Defines global publishing behavior for Pigeon producers.
    /// </summary>
    public class PublishingSettings
    {
        /// <summary>
        /// Gets or sets how direct broker publishing behaves when an ambient transaction is active.
        /// Defaults to suppressing the ambient transaction for direct broker publishes.
        /// </summary>
        public AmbientTransactionPublishBehavior AmbientTransactionBehavior { get; set; }
            = AmbientTransactionPublishBehavior.SuppressTransaction;
    }
}
