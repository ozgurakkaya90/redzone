using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260508100000_AddRiskLibrary")]
    public partial class AddRiskLibrary : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `RiskLibraryItems` (
    `Id`                int NOT NULL AUTO_INCREMENT,
    `Title`             varchar(300) NOT NULL,
    `Description`       longtext NULL,
    `Category`          varchar(100) NULL,
    `SubCategory`       varchar(100) NULL,
    `SourceType`        varchar(20) NOT NULL DEFAULT 'internal',
    `Hazard`            varchar(500) NULL,
    `PossibleImpact`    varchar(500) NULL,
    `RiskStrategy`      varchar(50) NULL,
    `Tags`              varchar(500) NULL,
    `ReferenceStandard` varchar(200) NULL,
    `IsActive`          tinyint(1) NOT NULL DEFAULT 1,
    `UsageCount`        int NOT NULL DEFAULT 0,
    `CreatedAt`         datetime(6) NOT NULL,
    `CreatedById`       int NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_RiskLibraryItems_Category` (`Category`),
    INDEX `IX_RiskLibraryItems_IsActive` (`IsActive`),
    CONSTRAINT `FK_RiskLibraryItems_Users_CreatedById`
        FOREIGN KEY (`CreatedById`) REFERENCES `Users`(`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4;");
            }
            else
            {
                // SQLite
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""RiskLibraryItems"" (
    ""Id""                INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""Title""             TEXT NOT NULL,
    ""Description""       TEXT NULL,
    ""Category""          TEXT NULL,
    ""SubCategory""       TEXT NULL,
    ""SourceType""        TEXT NOT NULL DEFAULT 'internal',
    ""Hazard""            TEXT NULL,
    ""PossibleImpact""    TEXT NULL,
    ""RiskStrategy""      TEXT NULL,
    ""Tags""              TEXT NULL,
    ""ReferenceStandard"" TEXT NULL,
    ""IsActive""          INTEGER NOT NULL DEFAULT 1,
    ""UsageCount""        INTEGER NOT NULL DEFAULT 0,
    ""CreatedAt""         TEXT NOT NULL,
    ""CreatedById""       INTEGER NULL
);
CREATE INDEX IF NOT EXISTS ""IX_RiskLibraryItems_Category"" ON ""RiskLibraryItems""(""Category"");
CREATE INDEX IF NOT EXISTS ""IX_RiskLibraryItems_IsActive"" ON ""RiskLibraryItems""(""IsActive"");");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql("DROP TABLE IF EXISTS `RiskLibraryItems`;");
            else
                migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""RiskLibraryItems"";");
        }
    }
}
