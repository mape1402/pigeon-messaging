namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Controls how a decision observer failure affects message settlement.</summary>
    public enum ObserverFailureBehavior
    {
        /// <summary>Stop observation and fail processing.</summary>
        Fail = 0,
        /// <summary>Log the failure and continue observation and settlement.</summary>
        Continue = 1
    }
}
