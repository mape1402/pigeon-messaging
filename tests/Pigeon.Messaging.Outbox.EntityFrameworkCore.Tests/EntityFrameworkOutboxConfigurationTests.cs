using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pigeon.Messaging.Outbox;
using Pigeon.Messaging.Outbox.EntityFrameworkCore.Internal;
using Mule;

#pragma warning disable EF1001

namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Tests;

public class EntityFrameworkOutboxConfigurationTests
{
    [Fact]
    public void UseEntityFrameworkOutbox_RejectsNullBuilder()
        => Assert.Throws<ArgumentNullException>(() => EntityFrameworkOutboxExtensions.UseEntityFrameworkOutbox<LegacyOutboxDbContext>(null));

    [Fact]
    public void UseEntityFrameworkOutbox_RequiresRegisteredDbContext()
    {
        var services = new ServiceCollection().AddLogging();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Pigeon:Domain"] = "test" }).Build();
        Assert.Throws<InvalidOperationException>(() => services.AddPigeon(config, builder => builder.UseEntityFrameworkOutbox<LegacyOutboxDbContext>()));
    }

    [Fact]
    public void UseEntityFrameworkOutbox_InvokesOptionsConfiguration()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<LegacyOutboxDbContext>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Pigeon:Domain"] = "test" }).Build();
        var called = false;
        services.AddPigeon(config, builder => builder.UseEntityFrameworkOutbox<LegacyOutboxDbContext>(_ => called = true));
        Assert.True(called);
    }

    [Fact]
    public void ResolveOptions_UsesRegisteredInstance()
    {
        var options = new DbContextOptionsBuilder<LegacyOutboxDbContext>().Options;
        var descriptor = ServiceDescriptor.Singleton(options);
        var method = typeof(EntityFrameworkOutboxExtensions).GetMethod("ResolveOptions", BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(typeof(LegacyOutboxDbContext));
        using var services = new ServiceCollection().BuildServiceProvider();
        Assert.Same(options, method.Invoke(null, new object[] { descriptor, services }));
    }

    [Fact]
    public void UseEntityFrameworkOutbox_AppliesModelToOptionsInstance()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(new DbContextOptionsBuilder<LegacyOutboxDbContext>().Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Pigeon:Domain"] = "test" }).Build();
        services.AddPigeon(config, builder => builder.UseEntityFrameworkOutbox<LegacyOutboxDbContext>());
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<DbContextOptions<LegacyOutboxDbContext>>();

        Assert.NotNull(options.FindExtension<PigeonOutboxOptionsExtension>());
    }

    [Fact]
    public void UseEntityFrameworkOutbox_AppliesModelToOptionsFactory()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<LegacyOutboxDbContext>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["Pigeon:Domain"] = "test" }).Build();
        services.AddPigeon(config, builder => builder.UseEntityFrameworkOutbox<LegacyOutboxDbContext>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<LegacyOutboxDbContext>>();

        Assert.NotNull(options.FindExtension<PigeonOutboxOptionsExtension>());
    }

    [Fact]
    public void Factory_RejectsNullDependencies()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var options = new DbContextOptionsBuilder<LegacyOutboxDbContext>().Options;
        Assert.Throws<ArgumentNullException>(() => new OutboxDbContextFactory<LegacyOutboxDbContext>(null, options));
        Assert.Throws<ArgumentNullException>(() => new OutboxDbContextFactory<LegacyOutboxDbContext>(services, null));
    }

    [Fact]
    public void Diagnostics_RejectsNullFactory()
        => Assert.Throws<ArgumentNullException>(() => new EntityFrameworkOutboxDiagnostics<LegacyOutboxDbContext>(null));

    [Fact]
    public async Task Diagnostics_IgnoresActionsForOtherKeys()
    {
        using var fixture = new LegacyOutboxFixture();
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<DurableAction>().Add(new DurableAction { Id = Guid.NewGuid(), Key = ActionKey.From("other"), Payload = "{}", PayloadType = "test" });
            await db.SaveChangesAsync();
        }

        var snapshot = await new EntityFrameworkOutboxDiagnostics<LegacyOutboxDbContext>(fixture).GetSnapshotAsync();
        Assert.Equal(0, snapshot.PendingMessages);
        Assert.Null(snapshot.LastFailure);
    }

    [Fact]
    public async Task Diagnostics_ReportsNewestFailureAndOldestFailedTime()
    {
        using var fixture = new LegacyOutboxFixture();
        var now = DateTimeOffset.UtcNow;
        await using (var db = fixture.CreateDbContext())
        {
            db.Set<DurableAction>().Add(new DurableAction { Id = Guid.NewGuid(), Key = PigeonOutboxActionKeys.Publish, Payload = "{}", PayloadType = "test", Status = DurableActionStatus.Failed, CreatedOnUtc = now.AddMinutes(-2), LastError = "old" });
            db.Set<DurableAction>().Add(new DurableAction { Id = Guid.NewGuid(), Key = PigeonOutboxActionKeys.Publish, Payload = "{}", PayloadType = "test", Status = DurableActionStatus.Failed, CreatedOnUtc = now.AddMinutes(-1), LastError = "new" });
            await db.SaveChangesAsync();
        }
        var snapshot = await new EntityFrameworkOutboxDiagnostics<LegacyOutboxDbContext>(fixture).GetSnapshotAsync();
        Assert.Equal("new", snapshot.LastFailure);
        Assert.Equal(now.AddMinutes(-2), snapshot.OldestFailedMessageOnUtc);
    }

    [Fact]
    public void OptionsExtension_IdentifiesEquivalentExtensions()
    {
        var first = new PigeonOutboxOptionsExtension();
        var second = new PigeonOutboxOptionsExtension();
        Assert.True(first.Info.ShouldUseSameServiceProvider(second.Info));
        Assert.Same(first.Info, first.Info);
    }
}
