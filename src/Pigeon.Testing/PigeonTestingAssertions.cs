namespace Pigeon.Testing
{
    /// <summary>
    /// Assertion helpers for Pigeon testing transport results.
    /// </summary>
    public static class PigeonTestingAssertions
    {
        /// <summary>
        /// Asserts that a published message of type <typeparamref name="T"/> exists.
        /// </summary>
        public static PigeonTestingMessage<T> ShouldContainMessage<T>(this IPigeonTestingTransport pigeon) where T : class
            => pigeon.ShouldContainMessage<T>(_ => true);

        /// <summary>
        /// Asserts that a published message of type <typeparamref name="T"/> matching the predicate exists.
        /// </summary>
        public static PigeonTestingMessage<T> ShouldContainMessage<T>(
            this IPigeonTestingTransport pigeon,
            Func<T, bool> predicate)
            where T : class
        {
            if (pigeon == null)
                throw new ArgumentNullException(nameof(pigeon));

            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            var message = pigeon.PublishedMessages
                .FirstOrDefault(item => item.Message is T typed && predicate(typed));

            if (message == null)
                throw new PigeonTestingAssertionException($"Expected a published message of type '{typeof(T).Name}', but none was found.");

            return new PigeonTestingMessage<T>(message);
        }

        /// <summary>
        /// Asserts that a consumed message of type <typeparamref name="T"/> exists.
        /// </summary>
        public static PigeonTestingMessage<T> ShouldContainConsumedMessage<T>(this IPigeonTestingTransport pigeon) where T : class
            => pigeon.ShouldContainConsumedMessage<T>(_ => true);

        /// <summary>
        /// Asserts that a consumed message of type <typeparamref name="T"/> matching the predicate exists.
        /// </summary>
        public static PigeonTestingMessage<T> ShouldContainConsumedMessage<T>(
            this IPigeonTestingTransport pigeon,
            Func<T, bool> predicate)
            where T : class
        {
            var message = pigeon.ConsumedMessages
                .FirstOrDefault(item => item.Message is T typed && predicate(typed));

            if (message == null)
                throw new PigeonTestingAssertionException($"Expected a consumed message of type '{typeof(T).Name}', but none was found.");

            return new PigeonTestingMessage<T>(message);
        }

        /// <summary>
        /// Asserts that a dead-letter message of type <typeparamref name="T"/> exists.
        /// </summary>
        public static PigeonTestingMessage<T> ShouldContainDeadLetterMessage<T>(this IPigeonTestingTransport pigeon) where T : class
            => pigeon.ShouldContainDeadLetterMessage<T>(_ => true);

        /// <summary>
        /// Asserts that a dead-letter message of type <typeparamref name="T"/> matching the predicate exists.
        /// </summary>
        public static PigeonTestingMessage<T> ShouldContainDeadLetterMessage<T>(
            this IPigeonTestingTransport pigeon,
            Func<T, bool> predicate)
            where T : class
        {
            var message = pigeon.DeadLetterMessages
                .FirstOrDefault(item => item.Message is T typed && predicate(typed));

            if (message == null)
                throw new PigeonTestingAssertionException($"Expected a dead-letter message of type '{typeof(T).Name}', but none was found.");

            return new PigeonTestingMessage<T>(message);
        }

        /// <summary>
        /// Asserts that a failure for message type <typeparamref name="T"/> exists.
        /// </summary>
        public static PigeonTestingFailure ShouldHaveConsumerFailure<T>(this IPigeonTestingTransport pigeon) where T : class
            => pigeon.ShouldHaveConsumerFailure<T>(_ => true);

        /// <summary>
        /// Asserts that a failure for message type <typeparamref name="T"/> matching the predicate exists.
        /// </summary>
        public static PigeonTestingFailure ShouldHaveConsumerFailure<T>(
            this IPigeonTestingTransport pigeon,
            Func<T, bool> predicate)
            where T : class
        {
            var failure = pigeon.ConsumerFailures
                .FirstOrDefault(item => item.Message.Message is T typed && predicate(typed));

            if (failure == null)
                throw new PigeonTestingAssertionException($"Expected a failure for message type '{typeof(T).Name}', but none was found.");

            return failure;
        }

        /// <summary>
        /// Asserts that a value equals an expected value.
        /// </summary>
        public static T ShouldBe<T>(this T actual, T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
                throw new PigeonTestingAssertionException($"Expected '{expected}', but found '{actual}'.");

            return actual;
        }
    }
}
