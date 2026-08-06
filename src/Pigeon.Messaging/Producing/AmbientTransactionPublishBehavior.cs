namespace Pigeon.Messaging.Producing
{
    /// <summary>
    /// Defines how direct broker publishing behaves when an ambient transaction is active.
    /// </summary>
    public enum AmbientTransactionPublishBehavior
    {
        /// <summary>
        /// Suppresses the ambient transaction while publishing directly to the broker.
        /// </summary>
        SuppressTransaction = 0,

        /// <summary>
        /// Throws when direct broker publishing is attempted inside an ambient transaction.
        /// </summary>
        Throw = 1
    }
}
