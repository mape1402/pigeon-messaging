namespace Pigeon.Messaging.Outbox.InMemory
{
    using Pigeon.Messaging.Outbox;

    internal sealed class InMemoryOutboxSchemaInitializer : IOutboxSchemaInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
