namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Defines how the outbox storage schema should be managed by a provider.
    /// </summary>
    public enum OutboxSchemaMode
    {
        /// <summary>
        /// The provider creates the outbox schema automatically when the app starts.
        /// </summary>
        AutoCreate = 0,

        /// <summary>
        /// The application is responsible for creating the outbox schema.
        /// </summary>
        Migrations = 1
    }
}
