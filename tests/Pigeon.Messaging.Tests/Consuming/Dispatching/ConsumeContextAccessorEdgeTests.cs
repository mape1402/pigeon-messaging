namespace Pigeon.Messaging.Tests.Consuming.Dispatching;

using Pigeon.Messaging.Consuming.Dispatching;

public class ConsumeContextAccessorEdgeTests
{
    [Fact]
    public void DisposeTwice_DoesNotOverwriteCurrentContext()
    {
        var accessor = new ConsumeContextAccessor();
        var first = accessor.Push(new ConsumeContext());
        first.Dispose();
        var current = new ConsumeContext();
        using (accessor.Push(current))
        {
            first.Dispose();
            Assert.Same(current, accessor.ConsumeContext);
        }
        Assert.Null(accessor.ConsumeContext);
    }
}
