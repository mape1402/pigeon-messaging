namespace Pigeon.Messaging.Tests.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pigeon.Messaging;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Producing.Management;
using System.Reflection;

internal sealed class OutboxDispatcherFixture : IDisposable
{
    private readonly ServiceProvider _services;
    public IOutboxStorage Storage { get; } = Substitute.For<IOutboxStorage>();
    public IOutboxDispatchQueue Queue { get; } = Substitute.For<IOutboxDispatchQueue>();
    public IProducingManager Producer { get; } = Substitute.For<IProducingManager>();
    public IOutboxSchemaInitializer Initializer { get; } = Substitute.For<IOutboxSchemaInitializer>();
    public OutboxSettings Settings { get; } = new() { Enabled = true };
    public OutboxDispatcherHostedService Dispatcher { get; }

    public OutboxDispatcherFixture(bool includeStorage = true)
    {
        var services = new ServiceCollection().AddSingleton(Producer).AddSingleton(Initializer);
        if (includeStorage) services.AddSingleton(Storage);
        _services = services.BuildServiceProvider();
        Dispatcher = new OutboxDispatcherHostedService(_services.GetRequiredService<IServiceScopeFactory>(), Queue,
            Options.Create(new GlobalSettings { Outbox = Settings }), NullLogger<OutboxDispatcherHostedService>.Instance);
    }

    public Task InvokeAsync(string name, params object[] arguments)
        => (Task)typeof(OutboxDispatcherHostedService).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(Dispatcher, arguments)!;

    public void Dispose() => _services.Dispose();
}
