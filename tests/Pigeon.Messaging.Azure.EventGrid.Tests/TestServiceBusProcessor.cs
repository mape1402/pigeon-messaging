namespace Pigeon.Messaging.Azure.EventGrid.Tests;

using global::Azure.Messaging.ServiceBus;

internal sealed class TestServiceBusProcessor : ServiceBusProcessor
{
    public override Task StartProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public override Task StopProcessingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeliverAsync(ProcessMessageEventArgs args) => OnProcessMessageAsync(args);
    public Task ReportErrorAsync(ProcessErrorEventArgs args) => OnProcessErrorAsync(args);
}
