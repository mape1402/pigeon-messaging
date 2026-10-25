namespace Pigeon.Testing.Tests;

using Pigeon.Messaging.Producing;

public class PigeonTestingFailureValidationTests
{
    [Fact]
    public void Constructor_RequiresMessage()
        => Assert.Throws<ArgumentNullException>(() => new PigeonTestingFailure(null, new InvalidOperationException(), PigeonTestingFailureKind.Consumer));

    [Fact]
    public void Constructor_RequiresException()
    {
        var message = new PigeonTestingMessage("hello", "orders", PublishingRoute.ForTopic("orders"), null, "{}", true, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentNullException>(() => new PigeonTestingFailure(message, null, PigeonTestingFailureKind.Consumer));
    }

    [Fact]
    public void Message_WithNullPayloadAndHeadersUsesEmptyDefaults()
    {
        var message = new PigeonTestingMessage(null, "orders", PublishingRoute.ForTopic("orders"), null, "{}", false, DateTimeOffset.UtcNow);
        Assert.Null(message.MessageType);
        Assert.Empty(message.Headers);
        Assert.NotEqual(Guid.Empty, message.Id);
    }
}
