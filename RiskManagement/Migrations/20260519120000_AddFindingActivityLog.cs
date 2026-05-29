using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260519120000_AddFindingActivityLog")]
    public partial class AddFindingActivityLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `FindingActivityLogs` (
    `Id`        INT NOT NULL AUTO_INCREMENT,
    `FindingId` INT NOT NULL,
    `UserId`    INT NULL,
    `Action`    VARCHAR(200) NOT NULL,
    `Detail`    VARCHAR(500) NULL,
    `Timestamp` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_FindingActivityLogs_AuditFindings_FindingId`
        FOREIGN KEY (`FindingId`) REFERENCES `AuditFindings`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_FindingActivityLogs_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4;");
            }
            else
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""FindingActivityLogs"" (
    ""Id""        INTEGER PRIMARY KEY AUTOINCREMENT,
    ""FindingId"" INTEGER NOT NULL REFERENCES ""AuditFindings""(""Id"") ON DELETE CASCADE,
    ""UserId""    INTEGER NULL REFERENCES ""Users""(""Id"") ON DELETE SET NULL,
    ""Action""    TEXT NOT NULL,
    ""Detail""    TEXT NULL,
    ""Timestamp"" TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql("DROP TABLE IF EXISTS `FindingActivityLogs`;");
            else
                migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""FindingActivityLogs"";");
        }
    }
}
