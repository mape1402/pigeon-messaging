namespace Pigeon.Messaging.Consuming.Management
{
    internal sealed class ConsumerExecutionDiagnostics : IConsumerExecutionDiagnostics
    {
        private long _receivedMessages;
        private long _queuedMessages;
        private long _activeHandlers;
        private long _completedHandlers;
        private long _failedHandlers;
        private long _acknowledgedMessages;
        private long _rejectedMessages;
        private long _queueWaitTicks;
        private long _queueWaitSamples;

        private bool _isQueueBounded;
        private int? _queueCapacity;
        private int? _maxConcurrency;
        private ushort? _prefetchCount;

        public ConsumerExecutionDiagnosticsSnapshot GetSnapshot()
        {
            var queueWaitSamples = Interlocked.Read(ref _queueWaitSamples);
            var queueWaitTicks = Interlocked.Read(ref _queueWaitTicks);

            return new ConsumerExecutionDiagnosticsSnapshot
            {
                ReceivedMessages = Interlocked.Read(ref _receivedMessages),
                QueuedMessages = Interlocked.Read(ref _queuedMessages),
                ActiveHandlers = Interlocked.Read(ref _activeHandlers),
                CompletedHandlers = Interlocked.Read(ref _completedHandlers),
                FailedHandlers = Interlocked.Read(ref _failedHandlers),
                AcknowledgedMessages = Interlocked.Read(ref _acknowledgedMessages),
                RejectedMessages = Interlocked.Read(ref _rejectedMessages),
                AverageQueueWait = queueWaitSamples > 0
                    ? TimeSpan.FromTicks(queueWaitTicks / queueWaitSamples)
                    : TimeSpan.Zero,
                IsQueueBounded = _isQueueBounded,
                QueueCapacity = _queueCapacity,
                MaxConcurrency = _maxConcurrency,
                PrefetchCount = _prefetchCount
            };
        }

        internal void Configure(ConsumerExecutionSettings settings)
        {
            var queueCapacity = settings?.QueueCapacity;
            var maxConcurrency = settings?.MaxConcurrency;

            _queueCapacity = queueCapacity > 0 ? queueCapacity : null;
            _isQueueBounded = _queueCapacity.HasValue;
            _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : null;
            _prefetchCount = settings?.PrefetchCount > 0
                ? settings.PrefetchCount
                : _maxConcurrency is > 0
                    ? (ushort)Math.Min(ushort.MaxValue, _maxConcurrency.Value)
                    : null;
        }

        internal void RecordReceived()
            => Interlocked.Increment(ref _receivedMessages);

        internal void RecordQueued()
            => Interlocked.Increment(ref _queuedMessages);

        internal void RecordQueueWriteFailed()
            => Interlocked.Decrement(ref _queuedMessages);

        internal void RecordDequeued(TimeSpan queueWait)
        {
            Interlocked.Decrement(ref _queuedMessages);
            Interlocked.Add(ref _queueWaitTicks, queueWait.Ticks);
            Interlocked.Increment(ref _queueWaitSamples);
        }

        internal void RecordHandlerStarted()
            => Interlocked.Increment(ref _activeHandlers);

        internal void RecordHandlerCompleted()
        {
            Interlocked.Decrement(ref _activeHandlers);
            Interlocked.Increment(ref _completedHandlers);
        }

        internal void RecordHandlerFailed()
        {
            Interlocked.Decrement(ref _activeHandlers);
            Interlocked.Increment(ref _failedHandlers);
        }

        internal void RecordAcknowledged()
            => Interlocked.Increment(ref _acknowledgedMessages);

        internal void RecordRejected()
            => Interlocked.Increment(ref _rejectedMessages);
    }
}
