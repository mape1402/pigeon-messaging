namespace Pigeon.Messaging.Tests.Consuming.Management;

using Pigeon.Messaging.Consuming.Management;
using NSubstitute;

public class ConsumeBackgroundServiceValidationTests
{
    [Fact]
    public void Constructor_RequiresManager() => Assert.Throws<ArgumentNullException>(() => new ConsumeBackgroundService(null));

    [Fact]
    public async Task Start_ForwardsCancellationToken()
    {
        var manager = Substitute.For<IConsumingManager>();
        var service = new ConsumeBackgroundService(manager);
        using var source = new CancellationTokenSource();
        await service.StartAsync(source.Token);
        await manager.Received(1).StartAsync(source.Token);
    }
}
