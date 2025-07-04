namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Base class for all consumer implementations that handle messages.
    /// Provides access to the current <see cref="ConsumeContext"/> during message processing.
    /// </summary>
    public abstract class HubConsumer
    {
        /// <summary>
        /// Gets or sets the current <see cref="ConsumeContext"/> associated with the message being processed.
        /// This property is set internally by the consuming infrastructure before invoking consumer methods.
        /// </summary>
        public ConsumeContext Context { get; internal set; }
    }
}
