namespace Pigeon.Messaging.InMemory
{
    /// <summary>Configures the in-memory transport.</summary>
    public sealed class InMemoryBrokerSettings
    {
        /// <summary>Gets or sets the delay before redelivery of a failed message.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    }
}
