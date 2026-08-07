namespace Pigeon.Testing
{
    /// <summary>
    /// Describes where a testing dispatch failure came from.
    /// </summary>
    public enum PigeonTestingFailureKind
    {
        /// <summary>
        /// Failure simulated before the message reached a consumer.
        /// </summary>
        Infrastructure,

        /// <summary>
        /// Failure thrown by a registered consumer.
        /// </summary>
        Consumer
    }
}
