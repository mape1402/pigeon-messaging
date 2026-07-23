using System.Collections.Concurrent;

internal sealed class PublishedMessages
{
    private readonly ConcurrentQueue<PublishedEnvelope> _messages = new();

    public int Count => _messages.Count;

    public IReadOnlyCollection<PublishedEnvelope> Snapshot => _messages.ToArray();

    public void Add(PublishedEnvelope message)
        => _messages.Enqueue(message);

    public async Task WaitForCountAsync(int expectedCount, TimeSpan timeout)
    {
        var expiresOn = DateTimeOffset.UtcNow.Add(timeout);

        while (DateTimeOffset.UtcNow < expiresOn)
        {
            if (Count >= expectedCount)
                return;

            await Task.Delay(50);
        }

        throw new TimeoutException($"Expected {expectedCount} published message(s), but found {Count}.");
    }
}
