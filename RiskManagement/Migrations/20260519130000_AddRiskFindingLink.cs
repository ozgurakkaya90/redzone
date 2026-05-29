using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260519130000_AddRiskFindingLink")]
    public partial class AddRiskFindingLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `RiskFindingLinks` (
    `Id`          INT NOT NULL AUTO_INCREMENT,
    `RiskId`      INT NOT NULL,
    `FindingId`   INT NOT NULL,
    `CreatedById` INT NOT NULL,
    `CreatedAt`   DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_RiskFindingLinks_RiskId_FindingId` (`RiskId`, `FindingId`),
    CONSTRAINT `FK_RiskFindingLinks_Risks_RiskId`
        FOREIGN KEY (`RiskId`) REFERENCES `Risks`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RiskFindingLinks_AuditFindings_FindingId`
        FOREIGN KEY (`FindingId`) REFERENCES `AuditFindings`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RiskFindingLinks_Users_CreatedById`
        FOREIGN KEY (`CreatedById`) REFERENCES `Users`(`Id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4;");
            }
            else
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""RiskFindingLinks"" (
    ""Id""          INTEGER PRIMARY KEY AUTOINCREMENT,
    ""RiskId""      INTEGER NOT NULL REFERENCES ""Risks""(""Id"") ON DELETE CASCADE,
    ""FindingId""   INTEGER NOT NULL REFERENCES ""AuditFindings""(""Id"") ON DELETE CASCADE,
    ""CreatedById"" INTEGER NOT NULL REFERENCES ""Users""(""Id""),
    ""CreatedAt""   TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE (""RiskId"", ""FindingId"")
);");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql("DROP TABLE IF EXISTS `RiskFindingLinks`;");
            else
                migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RiskFindingLinks"";");
        }
    }
}
