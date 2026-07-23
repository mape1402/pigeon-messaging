using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing;
using Pigeon.Messaging.Producing.Management;
using System.Transactions;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    ["Pigeon:Domain"] = "transaction-scope-sample"
});

builder.Services.AddSingleton<PublishedMessages>();
builder.Services.AddSingleton<TransactionalInMemoryOutboxStore>();

builder.Services.AddPigeon(builder.Configuration, pigeon =>
{
    pigeon.ConfigureOutbox(outbox =>
    {
        outbox.Enabled = true;
        outbox.ImmediateDispatch = true;
        outbox.DispatchInterval = TimeSpan.FromMinutes(10);
        outbox.CleanInterval = TimeSpan.FromMinutes(10);
        outbox.SchemaMode = OutboxSchemaMode.Manual;
    });

    pigeon.AddFeature(feature =>
    {
        feature.Services.AddScoped<IOutboxStorage>(provider =>
            provider.GetRequiredService<TransactionalInMemoryOutboxStore>().CreateStorage());
        feature.Services.AddSingleton<IMessageBrokerProducingAdapter, CapturingProducingAdapter>();
    });
});

using var host = builder.Build();
await host.StartAsync();

var publishedMessages = host.Services.GetRequiredService<PublishedMessages>();
var outboxStore = host.Services.GetRequiredService<TransactionalInMemoryOutboxStore>();

Console.WriteLine("Pigeon TransactionScope outbox demo");
Console.WriteLine("-----------------------------------");
PrintState("Initial state", outboxStore, publishedMessages);

Console.WriteLine();
Console.WriteLine("1. Publishing inside a committed TransactionScope.");
await PublishInsideCommittedTransactionAsync(host.Services);
PrintState("After PublishAsync, before background dispatch wait", outboxStore, publishedMessages);
await publishedMessages.WaitForCountAsync(1, TimeSpan.FromSeconds(5));
PrintState("After commit dispatch", outboxStore, publishedMessages);

Console.WriteLine();
Console.WriteLine("2. Publishing inside a rolled back TransactionScope.");
await PublishInsideRolledBackTransactionAsync(host.Services);
await Task.Delay(TimeSpan.FromSeconds(1));
PrintState("After rollback", outboxStore, publishedMessages);

if (publishedMessages.Count != 1)
    throw new InvalidOperationException($"Expected only one committed message, but {publishedMessages.Count} messages were published.");

var published = publishedMessages.Snapshot.Single();
Console.WriteLine();
Console.WriteLine($"Published message: {published.MessageType} '{published.Description}' via topic '{published.Route.Topic}'.");
Console.WriteLine("Demo completed: rollback message was not persisted or dispatched.");

await host.StopAsync();

static async Task PublishInsideCommittedTransactionAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

    var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
    await producer.PublishAsync(new SampleMessage("committed"), "sample.transaction-scope");

    transaction.Complete();
}

static async Task PublishInsideRolledBackTransactionAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    using var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

    var producer = scope.ServiceProvider.GetRequiredService<IProducer>();
    await producer.PublishAsync(new SampleMessage("rolled-back"), "sample.transaction-scope");
}

static void PrintState(string label, TransactionalInMemoryOutboxStore outboxStore, PublishedMessages publishedMessages)
{
    var outboxSnapshot = outboxStore.Snapshot;

    Console.WriteLine($"{label}:");
    Console.WriteLine($"  Outbox rows: {outboxSnapshot.Count}");
    Console.WriteLine($"  Published messages: {publishedMessages.Count}");

    foreach (var message in outboxSnapshot)
        Console.WriteLine($"  - {message.Id:N} | {message.Status} | topic={message.Topic}");
}
