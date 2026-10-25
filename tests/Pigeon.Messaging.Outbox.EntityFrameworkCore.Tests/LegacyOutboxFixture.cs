using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Tests;

internal sealed class LegacyOutboxFixture : IOutboxDbContextFactory<LegacyOutboxDbContext>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly DbContextOptions<LegacyOutboxDbContext> _options;

    public LegacyOutboxFixture()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<LegacyOutboxDbContext>().UseSqlite(_connection).Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public LegacyOutboxDbContext CreateDbContext() => new(_options);
    public void Dispose() => _connection.Dispose();
}
