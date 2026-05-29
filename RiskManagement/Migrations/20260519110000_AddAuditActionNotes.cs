using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260519110000_AddAuditActionNotes")]
    public partial class AddAuditActionNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @addDelay = (SELECT COUNT(*) = 0 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'AuditFindingActions' AND COLUMN_NAME = 'DelayReason');
SET @addClosure = (SELECT COUNT(*) = 0 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'AuditFindingActions' AND COLUMN_NAME = 'ClosureNote');
SET @sql1 = IF(@addDelay,  'ALTER TABLE `AuditFindingActions` ADD COLUMN `DelayReason` varchar(500) NULL',  'SELECT 1');
SET @sql2 = IF(@addClosure,'ALTER TABLE `AuditFindingActions` ADD COLUMN `ClosureNote` varchar(1000) NULL', 'SELECT 1');
PREPARE stmt1 FROM @sql1; EXECUTE stmt1; DEALLOCATE PREPARE stmt1;
PREPARE stmt2 FROM @sql2; EXECUTE stmt2; DEALLOCATE PREPARE stmt2;");
            }
            else
            {
                migrationBuilder.Sql(@"ALTER TABLE ""AuditFindingActions"" ADD COLUMN ""DelayReason"" TEXT NULL;");
                migrationBuilder.Sql(@"ALTER TABLE ""AuditFindingActions"" ADD COLUMN ""ClosureNote"" TEXT NULL;");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                migrationBuilder.Sql(@"
ALTER TABLE `AuditFindingActions`
    DROP COLUMN IF EXISTS `DelayReason`,
    DROP COLUMN IF EXISTS `ClosureNote`;");
            }
        }
    }
}
