namespace Pigeon.Messaging.Tests.Consuming.Dispatching;

using Pigeon.Messaging.Consuming.Dispatching;

public class ConsumingDispatcherValidationTests
{
    [Fact]
    public void Constructor_RequiresServiceProvider() => Assert.Throws<ArgumentNullException>(() => new ConsumingDispatcher(null));
}
