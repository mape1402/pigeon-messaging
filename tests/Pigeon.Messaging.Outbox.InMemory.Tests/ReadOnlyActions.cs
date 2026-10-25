namespace Pigeon.Messaging.Outbox.InMemory.Tests;

using Mule;
using System.Collections;

internal sealed class ReadOnlyActions : IReadOnlyCollection<DurableAction>
{
    public int Count => 0;
    public IEnumerator<DurableAction> GetEnumerator() => Enumerable.Empty<DurableAction>().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
