using Pigeon.Messaging.Consuming.Management;

namespace Pigeon.Messaging.Tests.Consuming.Management;

public class MessageConsumedEventArgsSettlementTests
{
    [Fact]
    public async Task FailedSettlement_DoesNotPreventAnotherSettlementAttempt()
    {
        var attempts = 0;
        var message = new MessageConsumedEventArgs("orders", "{}", "billing",
            _ => ++attempts == 1 ? Task.FromException(new InvalidOperationException("broker failure")) : Task.CompletedTask,
            null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => message.CompleteAsync());
        await message.CompleteAsync();
        await message.CompleteAsync();
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ConcurrentSettlement_InvokesOnlyOneTransportCallback()
    {
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var message = new MessageConsumedEventArgs("orders", "{}", "billing",
            _ => { Interlocked.Increment(ref attempts); return pending.Task; },
            null);

        var first = message.CompleteAsync();
        await message.RetryAsync();
        pending.SetResult();
        await first;
        Assert.Equal(1, attempts);
    }
}
