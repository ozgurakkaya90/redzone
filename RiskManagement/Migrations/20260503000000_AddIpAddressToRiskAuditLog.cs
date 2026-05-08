using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddIpAddressToRiskAuditLog : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                // RiskAuditLogs — create if absent (includes IpAddress from the start)
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `RiskAuditLogs` (
    `Id`        int NOT NULL AUTO_INCREMENT,
    `RiskId`    int NOT NULL,
    `UserId`    int NULL,
    `Action`    longtext NOT NULL,
    `FieldName` longtext NULL,
    `OldValue`  longtext NULL,
    `NewValue`  longtext NULL,
    `IpAddress` varchar(45) NULL,
    `Timestamp` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RiskAuditLogs_Risks_RiskId`  FOREIGN KEY (`RiskId`) REFERENCES `Risks`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RiskAuditLogs_Users_UserId`   FOREIGN KEY (`UserId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4;");

                // Add IpAddress to existing tables — use PREPARE to safely skip if column exists
                migrationBuilder.Sql(@"
SET @col_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME   = 'RiskAuditLogs'
      AND COLUMN_NAME  = 'IpAddress'
);
SET @sql = IF(@col_exists = 0,
    'ALTER TABLE `RiskAuditLogs` ADD COLUMN `IpAddress` varchar(45) NULL',
    'SELECT 1');
PREPARE _stmt FROM @sql;
EXECUTE _stmt;
DEALLOCATE PREPARE _stmt;");

                // RiskReviews — create if absent
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `RiskReviews` (
    `Id`          int NOT NULL AUTO_INCREMENT,
    `RiskId`      int NOT NULL,
    `MeetingDate` datetime(6) NOT NULL,
    `Decision`    longtext NULL,
    `Notes`       longtext NULL,
    `CreatedById` int NOT NULL,
    `CreatedAt`   datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RiskReviews_Risks_RiskId`      FOREIGN KEY (`RiskId`)      REFERENCES `Risks`(`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_RiskReviews_Users_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `Users`(`Id`) ON DELETE RESTRICT
) CHARACTER SET utf8mb4;");
            }
            else
            {
                // SQLite
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""RiskAuditLogs"" (
    ""Id""        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""RiskId""    INTEGER NOT NULL,
    ""UserId""    INTEGER NULL,
    ""Action""    TEXT NOT NULL,
    ""FieldName"" TEXT NULL,
    ""OldValue""  TEXT NULL,
    ""NewValue""  TEXT NULL,
    ""IpAddress"" TEXT NULL,
    ""Timestamp"" TEXT NOT NULL
);");
                migrationBuilder.Sql(@"ALTER TABLE ""RiskAuditLogs"" ADD COLUMN ""IpAddress"" TEXT NULL;");
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""RiskReviews"" (
    ""Id""          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""RiskId""      INTEGER NOT NULL,
    ""MeetingDate"" TEXT NOT NULL,
    ""Decision""    TEXT NULL,
    ""Notes""       TEXT NULL,
    ""CreatedById"" INTEGER NOT NULL,
    ""CreatedAt""   TEXT NOT NULL
);");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql(@"
SET @sql = IF((SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='RiskAuditLogs' AND COLUMN_NAME='IpAddress')>0,
    'ALTER TABLE `RiskAuditLogs` DROP COLUMN `IpAddress`',
    'SELECT 1');
PREPARE _stmt FROM @sql; EXECUTE _stmt; DEALLOCATE PREPARE _stmt;");
        }
    }
}
