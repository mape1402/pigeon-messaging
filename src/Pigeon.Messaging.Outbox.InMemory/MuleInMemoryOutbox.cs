namespace Pigeon.Messaging.Outbox.InMemory
{
    using Mule;
    using Mule.InMemory;
    using Pigeon.Messaging.Outbox;

    internal sealed class MuleInMemoryOutbox : IInMemoryOutbox
    {
        private readonly IInMemoryMule _mule;
        private readonly IMuleSerializer _serializer;

        public MuleInMemoryOutbox(IInMemoryMule mule, IMuleSerializer serializer)
        {
            _mule = mule ?? throw new ArgumentNullException(nameof(mule));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public IReadOnlyCollection<OutboxMessage> Messages
            => _mule.Actions
                .Where(action => action.Key.Equals(PigeonOutboxActionKeys.Publish))
                .Select(ToOutboxMessage)
                .ToArray();

        public void Clear()
        {
            if (_mule.Actions is ICollection<DurableAction> collection)
            {
                collection.Clear();
                return;
            }

            var clearMethod = _mule.GetType().GetMethod("Clear");
            if (clearMethod != null)
                clearMethod.Invoke(_mule, Array.Empty<object>());
        }

        private OutboxMessage ToOutboxMessage(DurableAction action)
        {
            var message = (OutboxMessage)_serializer.Deserialize(action.Payload, typeof(OutboxMessage));

            message.Id = action.Id;
            message.Status = ToOutboxStatus(action.Status);
            message.Attempts = action.Attempts;
            message.LastError = action.LastError;
            message.CreatedOnUtc = action.CreatedOnUtc;
            message.LockedOnUtc = action.LockedOnUtc;
            message.NextAttemptOnUtc = action.NextAttemptOnUtc;
            message.PublishedOnUtc = action.CompletedOnUtc;

            return message;
        }

        private static OutboxMessageStatus ToOutboxStatus(DurableActionStatus status)
            => status switch
            {
                DurableActionStatus.Completed => OutboxMessageStatus.Published,
                DurableActionStatus.Failed => OutboxMessageStatus.Failed,
                DurableActionStatus.Locked => OutboxMessageStatus.Locked,
                _ => OutboxMessageStatus.Pending
            };
    }
}
