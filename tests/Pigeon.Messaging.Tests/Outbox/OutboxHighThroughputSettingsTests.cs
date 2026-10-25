namespace Pigeon.Messaging.Tests.Outbox;

using Pigeon.Messaging.Outbox;

public class OutboxHighThroughputSettingsTests
{
    [Fact]
    public void ConfigureHighThroughput_AppliesRecoveryDefaults()
    {
        var settings = new OutboxSettings { DispatchInterval = TimeSpan.Zero, YieldBetweenDrainBatches = TimeSpan.Zero, ImmediateDispatch = false };
        Assert.Same(settings, settings.ConfigureHighThroughput());
        Assert.Equal(TimeSpan.FromSeconds(5), settings.DispatchInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(1), settings.YieldBetweenDrainBatches);
        Assert.True(settings.ImmediateDispatch);
    }

    [Fact]
    public void ConfigureHighThroughput_PreservesShorterIntervals()
    {
        var settings = new OutboxSettings { DispatchInterval = TimeSpan.FromSeconds(1), YieldBetweenDrainBatches = TimeSpan.FromMilliseconds(20) };
        settings.ConfigureHighThroughput();
        Assert.Equal(TimeSpan.FromSeconds(1), settings.DispatchInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(20), settings.YieldBetweenDrainBatches);
    }
}
