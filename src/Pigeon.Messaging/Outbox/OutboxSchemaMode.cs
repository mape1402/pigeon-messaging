namespace Pigeon.Messaging.Outbox
{
    /// <summary>
    /// Defines how the outbox storage schema should be managed by a provider.
    /// </summary>
    public enum OutboxSchemaMode
    {
        AutoCreate = 0,
        Migrations = 1
    }
}
