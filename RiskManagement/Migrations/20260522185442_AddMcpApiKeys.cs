using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder mb)
        {
            var isMySql = mb.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                // Tablo yoksa oluştur
                mb.Sql(@"
                    CREATE TABLE IF NOT EXISTS `McpApiKeys` (
                        `Id`          int           NOT NULL AUTO_INCREMENT,
                        `Name`        varchar(100)  NOT NULL,
                        `KeyPrefix`   varchar(16)   NOT NULL,
                        `KeyHash`     longtext      NOT NULL,
                        `CreatedById` int           NOT NULL,
                        `CreatedAt`   datetime(6)   NOT NULL,
                        `LastUsedAt`  datetime(6)   NULL,
                        `IsActive`    tinyint(1)    NOT NULL,
                        CONSTRAINT `PK_McpApiKeys` PRIMARY KEY (`Id`),
                        CONSTRAINT `FK_McpApiKeys_Users_CreatedById`
                            FOREIGN KEY (`CreatedById`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
                    ) CHARACTER SET=utf8mb4;");

                // KeyPrefix TEXT/BLOB olarak oluşturulduysa varchar'a çevir
                mb.Sql(@"
                    SET @col_type := (
                        SELECT DATA_TYPE FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'McpApiKeys' AND COLUMN_NAME = 'KeyPrefix'
                    );
                    SET @fix := IF(@col_type IN ('text','mediumtext','longtext','blob','mediumblob','longblob'),
                        'ALTER TABLE `McpApiKeys` MODIFY COLUMN `KeyPrefix` varchar(16) NOT NULL',
                        'SELECT 1');
                    PREPARE s FROM @fix; EXECUTE s; DEALLOCATE PREPARE s;");

                // Index — KeyPrefix(16) ile oluştur (TEXT ve varchar her ikisi için de güvenli)
                mb.Sql(@"
                    SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'McpApiKeys' AND INDEX_NAME = 'IX_McpApiKeys_CreatedById');
                    SET @sql := IF(@idx = 0,
                        'CREATE INDEX `IX_McpApiKeys_CreatedById` ON `McpApiKeys` (`CreatedById`)',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                mb.Sql(@"
                    SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'McpApiKeys' AND INDEX_NAME = 'IX_McpApiKeys_KeyPrefix');
                    SET @sql := IF(@idx = 0,
                        'CREATE INDEX `IX_McpApiKeys_KeyPrefix` ON `McpApiKeys` (`KeyPrefix`(16))',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");
            }
            else
            {
                mb.Sql(@"
                    CREATE TABLE IF NOT EXISTS ""McpApiKeys"" (
                        ""Id""          INTEGER NOT NULL CONSTRAINT ""PK_McpApiKeys"" PRIMARY KEY AUTOINCREMENT,
                        ""Name""        TEXT    NOT NULL,
                        ""KeyPrefix""   TEXT    NOT NULL,
                        ""KeyHash""     TEXT    NOT NULL,
                        ""CreatedById"" INTEGER NOT NULL,
                        ""CreatedAt""   TEXT    NOT NULL,
                        ""LastUsedAt""  TEXT    NULL,
                        ""IsActive""    INTEGER NOT NULL,
                        CONSTRAINT ""FK_McpApiKeys_Users_CreatedById""
                            FOREIGN KEY (""CreatedById"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                    );");

                mb.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_McpApiKeys_CreatedById"" ON ""McpApiKeys"" (""CreatedById"");");
                mb.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_McpApiKeys_KeyPrefix""   ON ""McpApiKeys"" (""KeyPrefix"");");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder mb)
        {
            mb.DropTable(name: "McpApiKeys");
        }
    }
}
