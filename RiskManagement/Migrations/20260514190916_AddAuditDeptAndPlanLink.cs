using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddAuditDeptAndPlanLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql("ALTER TABLE `InternalAudits` ADD COLUMN IF NOT EXISTS `DepartmentId`    int NULL;");
                migrationBuilder.Sql("ALTER TABLE `InternalAudits` ADD COLUMN IF NOT EXISTS `AuditPlanItemId` int NULL;");
            }
            else
            {
                migrationBuilder.Sql(@"ALTER TABLE ""InternalAudits"" ADD COLUMN ""DepartmentId""    INTEGER NULL;");
                migrationBuilder.Sql(@"ALTER TABLE ""InternalAudits"" ADD COLUMN ""AuditPlanItemId"" INTEGER NULL;");
                migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_InternalAudits_DepartmentId""    ON ""InternalAudits""(""DepartmentId"");");
                migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_InternalAudits_AuditPlanItemId"" ON ""InternalAudits""(""AuditPlanItemId"");");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                migrationBuilder.Sql("ALTER TABLE `InternalAudits` DROP COLUMN IF EXISTS `AuditPlanItemId`;");
                migrationBuilder.Sql("ALTER TABLE `InternalAudits` DROP COLUMN IF EXISTS `DepartmentId`;");
            }
        }
    }
}
