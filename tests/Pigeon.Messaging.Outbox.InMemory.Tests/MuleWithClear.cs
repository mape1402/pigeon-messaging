namespace Pigeon.Messaging.Outbox.InMemory.Tests;

using Mule;
using Mule.InMemory;

internal sealed class MuleWithClear : IInMemoryMule
{
    public IReadOnlyCollection<DurableAction> Actions { get; } = new ReadOnlyActions();
    public bool Cleared { get; private set; }
    public void Clear() => Cleared = true;
}
