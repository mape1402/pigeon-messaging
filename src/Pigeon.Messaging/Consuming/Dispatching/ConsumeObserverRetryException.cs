namespace Pigeon.Messaging.Consuming.Dispatching
{
    internal sealed class ConsumeObserverRetryException : Exception
    {
        internal ConsumeObserverRetryException(Exception observerException, Exception retryException)
            : base("The consume decision observer failed and the transport could not request a retry.", retryException)
        {
            ObserverException = observerException;
        }

        internal Exception ObserverException { get; }
    }
}
