using System.Reflection;

namespace Pigeon.Messaging.Outbox.EntityFrameworkCore.Tests;

public class EntityFrameworkOutboxSchemaInitializerTests
{
    [Fact]
    public void Constructor_RejectsNullFactory()
        => Assert.Throws<ArgumentNullException>(() => new EntityFrameworkOutboxSchemaInitializer<LegacyOutboxDbContext>(null));

    [Fact]
    public async Task Initialize_IsIdempotentForSqlite()
    {
        using var fixture = new LegacyOutboxFixture();
        var initializer = new EntityFrameworkOutboxSchemaInitializer<LegacyOutboxDbContext>(fixture);
        await initializer.InitializeAsync();
        await initializer.InitializeAsync();
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "uniqueidentifier")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite", "INTEGER")]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "uuid")]
    [InlineData("Pomelo.EntityFrameworkCore.MySql", "longtext")]
    [InlineData("unknown", "")]
    [InlineData(null, "")]
    public void CreateTableSql_SelectsProviderSyntax(string provider, string expected)
    {
        var method = typeof(EntityFrameworkOutboxSchemaInitializer<LegacyOutboxDbContext>)
            .GetMethod("GetCreateTableSql", BindingFlags.NonPublic | BindingFlags.Static);
        var sql = (string)method.Invoke(null, new object[] { provider });
        if (expected.Length == 0)
            Assert.Empty(sql);
        else
            Assert.Contains(expected, sql);
    }
}
