namespace Pigeon.Messaging.Tests;

using Pigeon.Messaging;
using System.Text.Json;

public class WrappedPayloadJsonPropertyNamesTests
{
    [Theory]
    [InlineData(0, "domain")]
    [InlineData(1, "messageVersion")]
    [InlineData(2, "createdOnUtc")]
    [InlineData(3, "message")]
    [InlineData(4, "metadata")]
    public void Constructor_RequiresEveryPropertyName(int missing, string parameter)
    {
        var names = new[] { "Domain", "MessageVersion", "CreatedOnUtc", "Message", "Metadata" };
        names[missing] = null;
        var exception = Assert.Throws<ArgumentNullException>(() => new WrappedPayloadJsonPropertyNames(names[0], names[1], names[2], names[3], names[4]));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public void Policy_NullOptionsUsesDefaultPropertyNames()
    {
        var policy = WrappedPayloadJsonPolicy.Create((JsonSerializerOptions)null);
        Assert.Equal(WrappedPayloadJsonPolicy.Default.Name, policy.Name);
        Assert.Equal("Message", policy.PropertyNames.Message);
    }
}
