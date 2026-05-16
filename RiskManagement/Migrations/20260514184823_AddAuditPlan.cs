using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddAuditPlan : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `AuditPlans` (
    `Id`          int NOT NULL AUTO_INCREMENT,
    `Year`        int NOT NULL,
    `Title`       varchar(200) NOT NULL,
    `Description` longtext NULL,
    `CreatedById` int NOT NULL,
    `CreatedAt`   datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_AuditPlans_Year` (`Year`),
    INDEX `IX_AuditPlans_CreatedById` (`CreatedById`),
    CONSTRAINT `FK_AuditPlans_Users_CreatedById`
        FOREIGN KEY (`CreatedById`) REFERENCES `Users`(`Id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `AuditPlanItems` (
    `Id`               int NOT NULL AUTO_INCREMENT,
    `AuditPlanId`      int NOT NULL,
    `Title`            varchar(200) NOT NULL,
    `AuditType`        varchar(100) NULL,
    `AuditedUnit`      varchar(200) NULL,
    `Description`      varchar(500) NULL,
    `PlannedStartDate` date NOT NULL,
    `PlannedEndDate`   date NOT NULL,
    `ActualStartDate`  date NULL,
    `ActualEndDate`    date NULL,
    `ResponsibleId`    int NULL,
    `Notes`            varchar(500) NULL,
    `SortOrder`        int NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`),
    INDEX `IX_AuditPlanItems_AuditPlanId` (`AuditPlanId`),
    INDEX `IX_AuditPlanItems_ResponsibleId` (`ResponsibleId`),
    CONSTRAINT `FK_AuditPlanItems_AuditPlans_AuditPlanId`
        FOREIGN KEY (`AuditPlanId`) REFERENCES `AuditPlans`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_AuditPlanItems_Users_ResponsibleId`
        FOREIGN KEY (`ResponsibleId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4;");
            }
            else
            {
                // SQLite
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""AuditPlans"" (
    ""Id""          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""Year""        INTEGER NOT NULL,
    ""Title""       TEXT NOT NULL,
    ""Description"" TEXT NULL,
    ""CreatedById"" INTEGER NOT NULL,
    ""CreatedAt""   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS ""IX_AuditPlans_Year""       ON ""AuditPlans""(""Year"");
CREATE INDEX IF NOT EXISTS ""IX_AuditPlans_CreatedById"" ON ""AuditPlans""(""CreatedById"");

CREATE TABLE IF NOT EXISTS ""AuditPlanItems"" (
    ""Id""               INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""AuditPlanId""      INTEGER NOT NULL,
    ""Title""            TEXT NOT NULL,
    ""AuditType""        TEXT NULL,
    ""AuditedUnit""      TEXT NULL,
    ""Description""      TEXT NULL,
    ""PlannedStartDate"" TEXT NOT NULL,
    ""PlannedEndDate""   TEXT NOT NULL,
    ""ActualStartDate""  TEXT NULL,
    ""ActualEndDate""    TEXT NULL,
    ""ResponsibleId""    INTEGER NULL,
    ""Notes""            TEXT NULL,
    ""SortOrder""        INTEGER NOT NULL DEFAULT 0,
    CONSTRAINT ""FK_AuditPlanItems_AuditPlans_AuditPlanId""
        FOREIGN KEY (""AuditPlanId"") REFERENCES ""AuditPlans""(""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_AuditPlanItems_AuditPlanId""   ON ""AuditPlanItems""(""AuditPlanId"");
CREATE INDEX IF NOT EXISTS ""IX_AuditPlanItems_ResponsibleId"" ON ""AuditPlanItems""(""ResponsibleId"");");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                migrationBuilder.Sql("DROP TABLE IF EXISTS `AuditPlanItems`;");
                migrationBuilder.Sql("DROP TABLE IF EXISTS `AuditPlans`;");
            }
            else
            {
                migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AuditPlanItems"";");
                migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AuditPlans"";");
            }
        }
    }
}
