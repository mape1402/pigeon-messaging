namespace Pigeon.Messaging.Tests.Consuming.Management;

using Pigeon.Messaging.Consuming.Configuration;
using Pigeon.Messaging.Consuming.Management;

public class MessageConsumedEventArgsValidationTests
{
    [Fact]
    public void Constructor_RequiresTopic() => Assert.Throws<ArgumentNullException>(() => new MessageConsumedEventArgs(null, "{}"));

    [Fact]
    public void Constructor_RequiresPayload() => Assert.Throws<ArgumentNullException>(() => new MessageConsumedEventArgs("orders", null));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_DefaultsBlankSubscription(string subscription)
    {
        var message = new MessageConsumedEventArgs("orders", "{}", subscription);
        Assert.Equal(ConsumerEndpoint.DefaultSubscription, message.Subscription);
    }

    [Fact]
    public void Constructor_DoesNotPromiseRetryWithoutCallback()
    {
        var message = new MessageConsumedEventArgs("orders", "{}", "billing", null, null, null, null, true);
        Assert.False(message.HasRetryCallback);
    }

    [Fact]
    public void Constructor_RecognizesGenericFailureAsRetryCallback()
    {
        var message = new MessageConsumedEventArgs("orders", "{}", "billing", null, (_, _) => Task.CompletedTask, null, null, true);
        Assert.True(message.HasRetryCallback);
    }

    [Fact]
    public async Task EmptyCallbacks_AreSafe()
    {
        var message = new MessageConsumedEventArgs("orders", "{}");
        await message.RejectAsync();
    }
}
