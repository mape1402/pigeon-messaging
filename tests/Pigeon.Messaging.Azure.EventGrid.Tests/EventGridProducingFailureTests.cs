namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Pigeon.Messaging.Azure.EventGrid.Producing;
using Pigeon.Messaging.Producing;

public class EventGridProducingFailureTests
{
    [Fact]
    public async Task PublishRaw_PropagatesProviderFailure()
    {
        var provider = Substitute.For<IEventGridProvider>();
        var failure = new InvalidOperationException("unavailable");
        provider.GetClient("orders").Returns(_ => throw failure);
        var adapter = new EventGridProducingAdapter(provider, Substitute.For<ISerializer>(), NullLogger<EventGridProducingAdapter>.Instance);
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.PublishRawMessageAsync("hello", PublishingRoute.ForTopic("orders")).AsTask());
        Assert.Same(failure, thrown);
    }
}
