namespace Pigeon.Messaging.Tests.Outbox
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using NSubstitute;
    using Pigeon.Messaging.Outbox;
    using System.Transactions;

    public class AmbientTransactionOutboxCommitNotifierTests
    {
        [Fact]
        public async Task NotifySavedAsync_Should_Enqueue_Immediately_When_No_Transaction_Exists()
        {
            var queue = new TestOutboxDispatchQueue();
            var notifier = CreateNotifier(queue);
            var messageId = Guid.NewGuid();

            await notifier.NotifySavedAsync(messageId);

            Assert.Equal(messageId, await queue.WaitForMessageAsync());
        }

        [Fact]
        public async Task NotifySavedAsync_Should_Enqueue_After_Ambient_Transaction_Commits()
        {
            var queue = new TestOutboxDispatchQueue();
            var notifier = CreateNotifier(queue);
            var messageId = Guid.NewGuid();

            using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await notifier.NotifySavedAsync(messageId);

                Assert.False(queue.HasMessages);

                transaction.Complete();
            }

            Assert.Equal(messageId, await queue.WaitForMessageAsync());
        }

        [Fact]
        public async Task NotifySavedAsync_Should_Not_Enqueue_When_Ambient_Transaction_Rolls_Back()
        {
            var queue = new TestOutboxDispatchQueue();
            var notifier = CreateNotifier(queue);

            using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            {
                await notifier.NotifySavedAsync(Guid.NewGuid());
            }

            await Task.Delay(100);

            Assert.False(queue.HasMessages);
        }

        [Fact]
        public async Task NotifySavedAsync_Should_Not_Enqueue_When_Immediate_Dispatch_Is_Disabled()
        {
            var queue = new TestOutboxDispatchQueue();
            var notifier = CreateNotifier(queue, immediateDispatch: false);

            await notifier.NotifySavedAsync(Guid.NewGuid());

            await Task.Delay(100);

            Assert.False(queue.HasMessages);
        }

        private static AmbientTransactionOutboxCommitNotifier CreateNotifier(
            IOutboxDispatchQueue queue,
            bool immediateDispatch = true)
            => new(
                queue,
                Options.Create(new GlobalSettings
                {
                    Outbox = new OutboxSettings
                    {
                        Enabled = true,
                        ImmediateDispatch = immediateDispatch
                    }
                }),
                Substitute.For<ILogger<AmbientTransactionOutboxCommitNotifier>>());

        private sealed class TestOutboxDispatchQueue : IOutboxDispatchQueue
        {
            private readonly Queue<Guid> _messages = new();
            private readonly TaskCompletionSource<Guid> _messageReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool HasMessages
            {
                get
                {
                    lock (_messages)
                    {
                        return _messages.Count > 0;
                    }
                }
            }

            public ValueTask EnqueueAsync(Guid outboxMessageId, CancellationToken cancellationToken = default)
            {
                lock (_messages)
                {
                    _messages.Enqueue(outboxMessageId);
                }

                _messageReceived.TrySetResult(outboxMessageId);
                return ValueTask.CompletedTask;
            }

            public ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken = default)
            {
                lock (_messages)
                {
                    if (_messages.Count > 0)
                        return ValueTask.FromResult(_messages.Dequeue());
                }

                return new ValueTask<Guid>(_messageReceived.Task.WaitAsync(cancellationToken));
            }

            public async Task<Guid> WaitForMessageAsync()
                => await _messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }
}
