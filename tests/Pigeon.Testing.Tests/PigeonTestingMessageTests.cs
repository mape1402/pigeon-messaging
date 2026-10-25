namespace Pigeon.Testing.Tests;

public class PigeonTestingMessageTests
{
    [Fact]
    public void Constructor_DefaultsNullHeadersAndMessageType()
    {
        var message = new PigeonTestingMessage(null, "orders", null, null, "{}", false, DateTimeOffset.UtcNow);

        Assert.Null(message.MessageType);
        Assert.Empty(message.Headers);
        Assert.Null(message.CorrelationId);
    }

    [Fact]
    public void CorrelationId_HandlesNullHeaderValue()
    {
        var headers = new Dictionary<string, object> { ["correlation-id"] = null };
        var message = new PigeonTestingMessage(new object(), "orders", null, headers, "{}", false, DateTimeOffset.UtcNow);

        Assert.Null(message.CorrelationId);
    }

    [Fact]
    public void TypedMessage_RequiresSource() => Assert.Throws<ArgumentNullException>(() => new PigeonTestingMessage<object>(null));
}
