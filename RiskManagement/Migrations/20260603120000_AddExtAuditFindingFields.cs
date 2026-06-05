using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260603120000_AddExtAuditFindingFields")]
    public partial class AddExtAuditFindingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                // ExternalAudits: AuditType + ChecklistName
                migrationBuilder.Sql(@"
SET @col := (SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = 'ExternalAudits' AND column_name = 'AuditType');
SET @sql := IF(@col = 0,
    'ALTER TABLE `ExternalAudits` ADD COLUMN `AuditType` varchar(100) NULL AFTER `AuditingBody`',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = 'ExternalAudits' AND column_name = 'ChecklistName');
SET @sql := IF(@col = 0,
    'ALTER TABLE `ExternalAudits` ADD COLUMN `ChecklistName` varchar(300) NULL AFTER `Standard`',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                // AuditFindings: StandardArticle + StandardClause + NonconformityCount
                migrationBuilder.Sql(@"
SET @col := (SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = 'AuditFindings' AND column_name = 'StandardArticle');
SET @sql := IF(@col = 0,
    'ALTER TABLE `AuditFindings` ADD COLUMN `StandardArticle` varchar(500) NULL',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = 'AuditFindings' AND column_name = 'StandardClause');
SET @sql := IF(@col = 0,
    'ALTER TABLE `AuditFindings` ADD COLUMN `StandardClause` varchar(1000) NULL',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;

SET @col := (SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema = DATABASE() AND table_name = 'AuditFindings' AND column_name = 'NonconformityCount');
SET @sql := IF(@col = 0,
    'ALTER TABLE `AuditFindings` ADD COLUMN `NonconformityCount` int NULL',
    'SELECT 1');
PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");
            }
            else
            {
                // SQLite — ADD COLUMN 3.35+ desteklendiğinden doğrudan kullan
                migrationBuilder.Sql(@"
ALTER TABLE ""ExternalAudits"" ADD COLUMN ""AuditType"" TEXT NULL;
ALTER TABLE ""ExternalAudits"" ADD COLUMN ""ChecklistName"" TEXT NULL;
ALTER TABLE ""AuditFindings"" ADD COLUMN ""StandardArticle"" TEXT NULL;
ALTER TABLE ""AuditFindings"" ADD COLUMN ""StandardClause"" TEXT NULL;
ALTER TABLE ""AuditFindings"" ADD COLUMN ""NonconformityCount"" INTEGER NULL;");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                migrationBuilder.Sql(@"
ALTER TABLE `ExternalAudits` DROP COLUMN IF EXISTS `AuditType`;
ALTER TABLE `ExternalAudits` DROP COLUMN IF EXISTS `ChecklistName`;
ALTER TABLE `AuditFindings` DROP COLUMN IF EXISTS `StandardArticle`;
ALTER TABLE `AuditFindings` DROP COLUMN IF EXISTS `StandardClause`;
ALTER TABLE `AuditFindings` DROP COLUMN IF EXISTS `NonconformityCount`;");
            }
            // SQLite kolon drop için tablo yeniden oluşturma gerekir; atlandı.
        }
    }
}
