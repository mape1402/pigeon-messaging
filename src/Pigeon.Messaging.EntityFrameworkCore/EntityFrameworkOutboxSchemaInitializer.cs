namespace Pigeon.Messaging.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore;
    using Pigeon.Messaging.Outbox;

    internal sealed class EntityFrameworkOutboxSchemaInitializer<TDbContext> : IOutboxSchemaInitializer
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        public EntityFrameworkOutboxSchemaInitializer(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (!_dbContext.Database.IsRelational())
                return;

            var sql = GetCreateTableSql(_dbContext.Database.ProviderName);

            if (!string.IsNullOrWhiteSpace(sql))
                await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        private static string GetCreateTableSql(string providerName)
        {
            if (providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) == true)
            {
                return """
IF OBJECT_ID(N'[PigeonOutboxMessages]', N'U') IS NULL
BEGIN
    CREATE TABLE [PigeonOutboxMessages] (
        [Id] uniqueidentifier NOT NULL PRIMARY KEY,
        [Payload] nvarchar(max) NOT NULL,
        [PayloadType] nvarchar(1024) NOT NULL,
        [IsRaw] bit NOT NULL,
        [Topic] nvarchar(512) NULL,
        [Exchange] nvarchar(512) NULL,
        [RoutingKey] nvarchar(512) NULL,
        [Status] int NOT NULL,
        [Attempts] int NOT NULL,
        [LastError] nvarchar(max) NULL,
        [CreatedOnUtc] datetimeoffset NOT NULL,
        [LockedOnUtc] datetimeoffset NULL,
        [NextAttemptOnUtc] datetimeoffset NULL,
        [PublishedOnUtc] datetimeoffset NULL
    );
END
""";
            }

            if (providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            {
                return """
CREATE TABLE IF NOT EXISTS "PigeonOutboxMessages" (
    "Id" TEXT NOT NULL PRIMARY KEY,
    "Payload" TEXT NOT NULL,
    "PayloadType" TEXT NOT NULL,
    "IsRaw" INTEGER NOT NULL,
    "Topic" TEXT NULL,
    "Exchange" TEXT NULL,
    "RoutingKey" TEXT NULL,
    "Status" INTEGER NOT NULL,
    "Attempts" INTEGER NOT NULL,
    "LastError" TEXT NULL,
    "CreatedOnUtc" TEXT NOT NULL,
    "LockedOnUtc" TEXT NULL,
    "NextAttemptOnUtc" TEXT NULL,
    "PublishedOnUtc" TEXT NULL
);
""";
            }

            if (providerName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
            {
                return """
CREATE TABLE IF NOT EXISTS "PigeonOutboxMessages" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "Payload" text NOT NULL,
    "PayloadType" character varying(1024) NOT NULL,
    "IsRaw" boolean NOT NULL,
    "Topic" character varying(512) NULL,
    "Exchange" character varying(512) NULL,
    "RoutingKey" character varying(512) NULL,
    "Status" integer NOT NULL,
    "Attempts" integer NOT NULL,
    "LastError" text NULL,
    "CreatedOnUtc" timestamp with time zone NOT NULL,
    "LockedOnUtc" timestamp with time zone NULL,
    "NextAttemptOnUtc" timestamp with time zone NULL,
    "PublishedOnUtc" timestamp with time zone NULL
);
""";
            }

            if (providerName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true)
            {
                return """
CREATE TABLE IF NOT EXISTS `PigeonOutboxMessages` (
    `Id` char(36) NOT NULL PRIMARY KEY,
    `Payload` longtext NOT NULL,
    `PayloadType` varchar(1024) NOT NULL,
    `IsRaw` tinyint(1) NOT NULL,
    `Topic` varchar(512) NULL,
    `Exchange` varchar(512) NULL,
    `RoutingKey` varchar(512) NULL,
    `Status` int NOT NULL,
    `Attempts` int NOT NULL,
    `LastError` longtext NULL,
    `CreatedOnUtc` datetime(6) NOT NULL,
    `LockedOnUtc` datetime(6) NULL,
    `NextAttemptOnUtc` datetime(6) NULL,
    `PublishedOnUtc` datetime(6) NULL
);
""";
            }

            return string.Empty;
        }
    }
}
