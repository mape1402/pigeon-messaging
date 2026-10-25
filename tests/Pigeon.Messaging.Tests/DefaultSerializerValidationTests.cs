namespace Pigeon.Messaging.Tests;

public class DefaultSerializerValidationTests
{
    [Fact]
    public void Constructor_RequiresOptions() => Assert.Throws<ArgumentNullException>(() => new DefaultSerializer(null));
}
