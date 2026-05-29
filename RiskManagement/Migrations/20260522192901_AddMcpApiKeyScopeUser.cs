using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpApiKeyScopeUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder mb)
        {
            var isMySql = mb.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                // Kolon yoksa ekle
                mb.Sql(@"
                    SET @col := (SELECT COUNT(*) FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'McpApiKeys' AND COLUMN_NAME = 'ScopeUserId');
                    SET @sql := IF(@col = 0,
                        'ALTER TABLE `McpApiKeys` ADD COLUMN `ScopeUserId` int NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                // Index yoksa ekle
                mb.Sql(@"
                    SET @idx := (SELECT COUNT(*) FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'McpApiKeys' AND INDEX_NAME = 'IX_McpApiKeys_ScopeUserId');
                    SET @sql := IF(@idx = 0,
                        'CREATE INDEX `IX_McpApiKeys_ScopeUserId` ON `McpApiKeys` (`ScopeUserId`)',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                // FK yoksa ekle
                mb.Sql(@"
                    SET @fk := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                        WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'McpApiKeys'
                          AND CONSTRAINT_NAME = 'FK_McpApiKeys_Users_ScopeUserId');
                    SET @sql := IF(@fk = 0,
                        'ALTER TABLE `McpApiKeys` ADD CONSTRAINT `FK_McpApiKeys_Users_ScopeUserId` FOREIGN KEY (`ScopeUserId`) REFERENCES `Users` (`Id`) ON DELETE SET NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");
            }
            else
            {
                // SQLite — ALTER TABLE ADD COLUMN destekler ama IF NOT EXISTS yok
                // Kolon zaten varsa bu statement hata verir; try-catch gerekir
                // En basit yol: sadece ekle, uygulama zaten migration geçmişini kontrol eder
                mb.Sql(@"ALTER TABLE ""McpApiKeys"" ADD COLUMN ""ScopeUserId"" INTEGER NULL;");
                mb.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_McpApiKeys_ScopeUserId"" ON ""McpApiKeys"" (""ScopeUserId"");");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder mb)
        {
            var isMySql = mb.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                mb.Sql("ALTER TABLE `McpApiKeys` DROP FOREIGN KEY `FK_McpApiKeys_Users_ScopeUserId`;");
                mb.Sql("DROP INDEX `IX_McpApiKeys_ScopeUserId` ON `McpApiKeys`;");
                mb.Sql("ALTER TABLE `McpApiKeys` DROP COLUMN `ScopeUserId`;");
            }
            else
            {
                mb.DropIndex(name: "IX_McpApiKeys_ScopeUserId", table: "McpApiKeys");
                mb.DropColumn(name: "ScopeUserId", table: "McpApiKeys");
            }
        }
    }
}
