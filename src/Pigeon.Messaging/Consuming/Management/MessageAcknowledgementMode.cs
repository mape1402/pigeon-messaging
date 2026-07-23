namespace Pigeon.Messaging.Consuming.Management
{
    /// <summary>
    /// Defines who acknowledges broker messages after they are dispatched.
    /// </summary>
    public enum MessageAcknowledgementMode
    {
        /// <summary>
        /// Pigeon exposes acknowledgement operations to the handler and does not complete messages automatically.
        /// </summary>
        Manual = 0,

        /// <summary>
        /// The broker acknowledges the message as soon as it is delivered to Pigeon.
        /// Handler failures cannot roll the message back in this mode.
        /// </summary>
        OnReceive = 1,

        /// <summary>
        /// Pigeon completes messages when the handler finishes successfully and fails them when the handler throws.
        /// </summary>
        OnHandlerSuccess = 2
    }
}
