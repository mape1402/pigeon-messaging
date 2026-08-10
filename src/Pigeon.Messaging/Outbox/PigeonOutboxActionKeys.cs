namespace Pigeon.Messaging.Outbox
{
    using Mule;

    /// <summary>
    /// Contains durable action keys used by Pigeon outbox.
    /// </summary>
    public static class PigeonOutboxActionKeys
    {
        /// <summary>
        /// Durable action key used to publish a prepared Pigeon outbox message.
        /// </summary>
        public static readonly ActionKey Publish = ActionKey.From("pigeon.publish.v1");
    }
}
