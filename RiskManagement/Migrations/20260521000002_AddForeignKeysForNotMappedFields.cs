using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RiskManagement.Data;

#nullable disable

namespace RiskManagement.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521000002_AddForeignKeysForNotMappedFields")]
    public partial class AddForeignKeysForNotMappedFields : Migration
    {
        protected override void Up(MigrationBuilder mb)
        {
            var isMySql = mb.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                // ── Kolon ekle (MySQL'de IF NOT EXISTS yok — INFORMATION_SCHEMA kontrolü) ──
                // InternalAudits.DepartmentId
                mb.Sql(@"
                    SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InternalAudits' AND COLUMN_NAME = 'DepartmentId');
                    SET @sql := IF(@col_exists = 0,
                        'ALTER TABLE `InternalAudits` ADD COLUMN `DepartmentId` int NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                // InternalAudits.AuditPlanItemId
                mb.Sql(@"
                    SET @col_exists := (SELECT COUNT(*) FROM information_schema.COLUMNS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InternalAudits' AND COLUMN_NAME = 'AuditPlanItemId');
                    SET @sql := IF(@col_exists = 0,
                        'ALTER TABLE `InternalAudits` ADD COLUMN `AuditPlanItemId` int NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                // ── Index ekle (INFORMATION_SCHEMA kontrolü) ──────────────────────────────
                mb.Sql(@"
                    SET @idx_exists := (SELECT COUNT(*) FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InternalAudits' AND INDEX_NAME = 'IX_InternalAudits_DepartmentId');
                    SET @sql := IF(@idx_exists = 0,
                        'CREATE INDEX `IX_InternalAudits_DepartmentId` ON `InternalAudits` (`DepartmentId`)',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                mb.Sql(@"
                    SET @idx_exists := (SELECT COUNT(*) FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InternalAudits' AND INDEX_NAME = 'IX_InternalAudits_AuditPlanItemId');
                    SET @sql := IF(@idx_exists = 0,
                        'CREATE INDEX `IX_InternalAudits_AuditPlanItemId` ON `InternalAudits` (`AuditPlanItemId`)',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                mb.Sql(@"
                    SET @idx_exists := (SELECT COUNT(*) FROM information_schema.STATISTICS
                        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'AuditFindings' AND INDEX_NAME = 'IX_AuditFindings_ExternalAuditId');
                    SET @sql := IF(@idx_exists = 0,
                        'CREATE INDEX `IX_AuditFindings_ExternalAuditId` ON `AuditFindings` (`ExternalAuditId`)',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                // ── FK constraint ekle (INFORMATION_SCHEMA kontrolü) ─────────────────────
                mb.Sql(@"
                    SET @fk_exists := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                        WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'InternalAudits'
                          AND CONSTRAINT_NAME = 'FK_InternalAudits_Departments_DepartmentId');
                    SET @sql := IF(@fk_exists = 0,
                        'ALTER TABLE `InternalAudits` ADD CONSTRAINT `FK_InternalAudits_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE SET NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                mb.Sql(@"
                    SET @fk_exists := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                        WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'InternalAudits'
                          AND CONSTRAINT_NAME = 'FK_InternalAudits_AuditPlanItems_AuditPlanItemId');
                    SET @sql := IF(@fk_exists = 0,
                        'ALTER TABLE `InternalAudits` ADD CONSTRAINT `FK_InternalAudits_AuditPlanItems_AuditPlanItemId` FOREIGN KEY (`AuditPlanItemId`) REFERENCES `AuditPlanItems` (`Id`) ON DELETE SET NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");

                mb.Sql(@"
                    SET @fk_exists := (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                        WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'AuditFindings'
                          AND CONSTRAINT_NAME = 'FK_AuditFindings_ExternalAudits_ExternalAuditId');
                    SET @sql := IF(@fk_exists = 0,
                        'ALTER TABLE `AuditFindings` ADD CONSTRAINT `FK_AuditFindings_ExternalAudits_ExternalAuditId` FOREIGN KEY (`ExternalAuditId`) REFERENCES `ExternalAudits` (`Id`) ON DELETE SET NULL',
                        'SELECT 1');
                    PREPARE s FROM @sql; EXECUTE s; DEALLOCATE PREPARE s;");
            }
            else
            {
                // SQLite — EnsureCreated veya önceki migration ile kolonlar zaten oluşturuldu
                // FK constraint SQLite'da desteklenmez; index'ler idempotent eklenir
                mb.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_InternalAudits_DepartmentId""    ON ""InternalAudits""(""DepartmentId"");");
                mb.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_InternalAudits_AuditPlanItemId"" ON ""InternalAudits""(""AuditPlanItemId"");");
            }
        }

        protected override void Down(MigrationBuilder mb)
        {
            var isMySql = mb.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                mb.Sql("ALTER TABLE `InternalAudits` DROP FOREIGN KEY `FK_InternalAudits_Departments_DepartmentId`;");
                mb.Sql("ALTER TABLE `InternalAudits` DROP FOREIGN KEY `FK_InternalAudits_AuditPlanItems_AuditPlanItemId`;");
                mb.Sql("ALTER TABLE `AuditFindings`  DROP FOREIGN KEY `FK_AuditFindings_ExternalAudits_ExternalAuditId`;");
                mb.Sql("DROP INDEX `IX_InternalAudits_DepartmentId`    ON `InternalAudits`;");
                mb.Sql("DROP INDEX `IX_InternalAudits_AuditPlanItemId` ON `InternalAudits`;");
            }
        }
    }
}
