namespace Pigeon.Testing
{
    /// <summary>
    /// Exception thrown when a Pigeon testing assertion fails.
    /// </summary>
    public sealed class PigeonTestingAssertionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PigeonTestingAssertionException"/> class.
        /// </summary>
        public PigeonTestingAssertionException(string message) : base(message)
        {
        }
    }
}
