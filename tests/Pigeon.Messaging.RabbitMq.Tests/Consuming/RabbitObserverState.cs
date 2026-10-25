namespace Pigeon.Messaging.RabbitMq.Tests.Consuming;

internal sealed class RabbitObserverState
{
    public int Attempts;
    public TaskCompletionSource Recovered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}
