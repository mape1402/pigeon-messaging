namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>
    /// Provides access to the current consume context when code is running inside a Pigeon consume pipeline.
    /// </summary>
    public interface IConsumeContextAccessor
    {
        /// <summary>
        /// Gets the current consume context, or null when code is not running inside a consume pipeline.
        /// </summary>
        ConsumeContext ConsumeContext { get; }
    }
}
