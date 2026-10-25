namespace Pigeon.Messaging.Tests.Consuming.Configuration;

using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Dispatching;

public class ConsumerConfigurationValidationTests
{
    [Fact]
    public void Constructor_RequiresHandler()
        => Assert.Throws<ArgumentNullException>(() => new ConsumerConfiguration<string>(null));

    [Fact]
    public async Task Handler_ForwardsTypedMessage()
    {
        string received = null;
        var configuration = new ConsumerConfiguration<string>((_, message) => { received = message; return Task.CompletedTask; });
        await configuration.Handler(new ConsumeContext { Message = "hello" });
        Assert.Equal("hello", received);
    }
}
