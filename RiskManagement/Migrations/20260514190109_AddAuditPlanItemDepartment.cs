using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddAuditPlanItemDepartment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `DepartmentId` int NULL;");
            }
            else
            {
                migrationBuilder.Sql(@"ALTER TABLE ""AuditPlanItems"" ADD COLUMN ""DepartmentId"" INTEGER NULL;");
                migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_AuditPlanItems_DepartmentId"" ON ""AuditPlanItems""(""DepartmentId"");");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` DROP COLUMN IF EXISTS `DepartmentId`;");
        }
    }
}
