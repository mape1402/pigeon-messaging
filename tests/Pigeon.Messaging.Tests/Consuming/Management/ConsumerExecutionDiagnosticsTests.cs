namespace Pigeon.Messaging.Tests.Consuming.Management;

using Pigeon.Messaging.Consuming.Management;

public class ConsumerExecutionDiagnosticsTests
{
    [Fact]
    public void Configure_NullSettingsUsesUnboundedDefaults()
    {
        var diagnostics = new ConsumerExecutionDiagnostics();
        diagnostics.Configure(null);

        var snapshot = diagnostics.GetSnapshot();
        Assert.False(snapshot.IsQueueBounded);
        Assert.Null(snapshot.QueueCapacity);
        Assert.Null(snapshot.MaxConcurrency);
        Assert.Null(snapshot.PrefetchCount);
    }

    [Fact]
    public void Configure_DerivedPrefetchIsCappedAtUShortMax()
    {
        var diagnostics = new ConsumerExecutionDiagnostics();
        diagnostics.Configure(new ConsumerExecutionSettings { MaxConcurrency = 100000 });

        Assert.Equal(ushort.MaxValue, diagnostics.GetSnapshot().PrefetchCount);
    }

    [Fact]
    public void Configure_UsesExplicitPrefetch()
    {
        var diagnostics = new ConsumerExecutionDiagnostics();
        diagnostics.Configure(new ConsumerExecutionSettings { MaxConcurrency = 100, PrefetchCount = 7 });

        Assert.Equal((ushort)7, diagnostics.GetSnapshot().PrefetchCount);
    }
}
