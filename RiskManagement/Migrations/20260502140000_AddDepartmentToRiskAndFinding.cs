using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddDepartmentToRiskAndFinding : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
ALTER TABLE `Risks`
    ADD COLUMN IF NOT EXISTS `DepartmentId` int NULL,
    ADD CONSTRAINT IF NOT EXISTS `FK_Risks_Departments_DeptId`
        FOREIGN KEY (`DepartmentId`) REFERENCES `Departments`(`Id`) ON DELETE SET NULL;
");
                migrationBuilder.Sql(@"
ALTER TABLE `AuditFindings`
    ADD COLUMN IF NOT EXISTS `DepartmentId` int NULL,
    ADD CONSTRAINT IF NOT EXISTS `FK_AuditFindings_Departments_DeptId`
        FOREIGN KEY (`DepartmentId`) REFERENCES `Departments`(`Id`) ON DELETE SET NULL;
");
            }
            else
            {
                // SQLite doesn't support ADD CONSTRAINT in ALTER TABLE; FK is defined via CREATE
                migrationBuilder.Sql(@"ALTER TABLE ""Risks"" ADD COLUMN IF NOT EXISTS ""DepartmentId"" INTEGER NULL;");
                migrationBuilder.Sql(@"ALTER TABLE ""AuditFindings"" ADD COLUMN IF NOT EXISTS ""DepartmentId"" INTEGER NULL;");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                migrationBuilder.Sql("ALTER TABLE `Risks` DROP FOREIGN KEY IF EXISTS `FK_Risks_Departments_DeptId`, DROP COLUMN IF EXISTS `DepartmentId`;");
                migrationBuilder.Sql("ALTER TABLE `AuditFindings` DROP FOREIGN KEY IF EXISTS `FK_AuditFindings_Departments_DeptId`, DROP COLUMN IF EXISTS `DepartmentId`;");
            }
            else
            {
                // SQLite doesn't support DROP COLUMN easily; leave as-is
            }
        }
    }
}
