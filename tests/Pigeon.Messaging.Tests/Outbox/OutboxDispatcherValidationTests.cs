namespace Pigeon.Messaging.Tests.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;

public class OutboxDispatcherValidationTests
{
    [Theory]
    [InlineData(0, "scopeFactory")]
    [InlineData(1, "dispatchQueue")]
    [InlineData(2, "settings")]
    [InlineData(3, "logger")]
    public void Constructor_RequiresEachDependency(int missing, string parameter)
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new OutboxDispatcherHostedService(
            missing == 0 ? null : Substitute.For<IServiceScopeFactory>(),
            missing == 1 ? null : Substitute.For<IOutboxDispatchQueue>(),
            missing == 2 ? null : Options.Create(new GlobalSettings()),
            missing == 3 ? null : NullLogger<OutboxDispatcherHostedService>.Instance));
        Assert.Equal(parameter, exception.ParamName);
    }

    [Fact]
    public async Task DisabledOutbox_DoesNotReadQueue()
    {
        var queue = Substitute.For<IOutboxDispatchQueue>();
        using var service = new OutboxDispatcherHostedService(Substitute.For<IServiceScopeFactory>(), queue,
            Options.Create(new GlobalSettings()), NullLogger<OutboxDispatcherHostedService>.Instance);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        await queue.DidNotReceive().DequeueAsync(Arg.Any<CancellationToken>());
    }
}
