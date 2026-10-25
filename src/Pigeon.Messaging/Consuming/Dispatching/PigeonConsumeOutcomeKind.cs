namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Describes the semantic meaning of a consume decision.</summary>
    public enum PigeonConsumeOutcomeKind
    {
        /// <summary>No semantic outcome was specified.</summary>
        Unspecified = 0,
        /// <summary>Processing was skipped.</summary>
        Skipped = 1,
        /// <summary>The message was already processed.</summary>
        AlreadyProcessed = 2,
        /// <summary>The message was rejected.</summary>
        Rejected = 3,
        /// <summary>A retry was requested.</summary>
        RetryRequested = 4,
        /// <summary>Processing was deferred.</summary>
        Deferred = 5
    }
}
