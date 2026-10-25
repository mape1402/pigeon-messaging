namespace Pigeon.Messaging.Consuming.Dispatching
{
    /// <summary>Indicates an observer failed and the transport could not request redelivery.</summary>
    public sealed class ConsumeObserverRetryException : Exception
    {
        internal ConsumeObserverRetryException(Exception observerException, Exception retryException)
            : base("The consume decision observer failed and the transport could not request a retry.", retryException)
        {
            ObserverException = observerException;
        }

        /// <summary>Gets the original observer failure.</summary>
        public Exception ObserverException { get; }
    }
}
