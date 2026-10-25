namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using global::Azure;
using global::Azure.Messaging.EventGrid;
using NSubstitute;

public class EventGridPublisherTests
{
    private readonly EventGridPublisherClient _client = Substitute.For<EventGridPublisherClient>();
    private readonly AzureEventGridSettings _settings = new();

    [Fact]
    public void Constructor_RequiresSettings()
        => Assert.Throws<ArgumentNullException>(() => new EventGridPublisher(null, "orders", _client));

    [Fact]
    public void Constructor_RequiresTopic()
        => Assert.Throws<ArgumentNullException>(() => new EventGridPublisher(_settings, null, _client));

    [Fact]
    public void Constructor_RequiresClient()
        => Assert.Throws<ArgumentNullException>(() => new EventGridPublisher(_settings, "orders", null));

    [Fact]
    public async Task Publish_ForwardsEventsAndCancellation()
    {
        var events = new[] { new EventGridEvent("subject", "created", "1.0", new { Id = 1 }) };
        using var cancellation = new CancellationTokenSource();
        await new EventGridPublisher(_settings, "orders", _client).PublishCloudEventsAsync(events, cancellation.Token);
        await _client.Received(1).SendEventsAsync(events, cancellation.Token);
    }

    [Fact]
    public async Task Publish_WrapsClientFailureWithTopic()
    {
        var failure = new RequestFailedException("unavailable");
        _client.SendEventsAsync(Arg.Any<IEnumerable<EventGridEvent>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Response>(failure));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EventGridPublisher(_settings, "orders", _client).PublishCloudEventsAsync(Array.Empty<EventGridEvent>()));
        Assert.Contains("orders", error.Message);
        Assert.Same(failure, error.InnerException);
    }
}
