using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260516120000_AddExternalAudit")]
    public partial class AddExternalAudit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `ExternalAuditBodies` (
    `Id`          int NOT NULL AUTO_INCREMENT,
    `Name`        varchar(200) NOT NULL,
    `Description` varchar(500) NULL,
    `IsActive`    tinyint(1) NOT NULL DEFAULT 1,
    `SortOrder`   int NOT NULL DEFAULT 0,
    `CreatedAt`   datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_ExternalAuditBodies_Name` (`Name`),
    INDEX `IX_ExternalAuditBodies_IsActive` (`IsActive`)
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `ExternalAudits` (
    `Id`                int NOT NULL AUTO_INCREMENT,
    `Code`              varchar(20) NOT NULL,
    `AuditingBody`      varchar(200) NOT NULL,
    `Standard`          varchar(100) NULL,
    `Subject`           varchar(300) NOT NULL,
    `AuditDate`         date NOT NULL,
    `EndDate`           date NULL,
    `ResponsibleDeptId` int NULL,
    `ResponsibleUserId` int NULL,
    `Status`            varchar(20) NOT NULL DEFAULT 'planned',
    `Score`             varchar(100) NULL,
    `ReportFilePath`    varchar(500) NULL,
    `Notes`             varchar(2000) NULL,
    `CreatedById`       int NOT NULL,
    `CreatedAt`         datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_ExternalAudits_AuditingBody`      (`AuditingBody`),
    INDEX `IX_ExternalAudits_Status`            (`Status`),
    INDEX `IX_ExternalAudits_AuditDate`         (`AuditDate`),
    INDEX `IX_ExternalAudits_CreatedById`       (`CreatedById`),
    INDEX `IX_ExternalAudits_ResponsibleDeptId` (`ResponsibleDeptId`),
    INDEX `IX_ExternalAudits_ResponsibleUserId` (`ResponsibleUserId`),
    CONSTRAINT `FK_ExternalAudits_Users_CreatedById`
        FOREIGN KEY (`CreatedById`) REFERENCES `Users`(`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_ExternalAudits_Departments_ResponsibleDeptId`
        FOREIGN KEY (`ResponsibleDeptId`) REFERENCES `Departments`(`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_ExternalAudits_Users_ResponsibleUserId`
        FOREIGN KEY (`ResponsibleUserId`) REFERENCES `Users`(`Id`) ON DELETE SET NULL
) CHARACTER SET utf8mb4;

CREATE TABLE IF NOT EXISTS `UserExternalAuditBodies` (
    `Id`           int NOT NULL AUTO_INCREMENT,
    `UserId`       int NOT NULL,
    `AuditingBody` varchar(200) NOT NULL,
    `CanEdit`      tinyint(1) NOT NULL DEFAULT 0,
    `CreatedAt`    datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_UserExternalAuditBodies_UserId_Body` (`UserId`,`AuditingBody`),
    CONSTRAINT `FK_UserExternalAuditBodies_Users_UserId`
        FOREIGN KEY (`UserId`) REFERENCES `Users`(`Id`) ON DELETE CASCADE
) CHARACTER SET utf8mb4;");

                // AuditFindings'e AuditSource ve ExternalAuditId ekle (idempotent)
                migrationBuilder.Sql(@"
SET @col_exists := (SELECT COUNT(*) FROM information_schema.columns
                    WHERE table_schema = DATABASE()
                      AND table_name = 'AuditFindings'
                      AND column_name = 'AuditSource');
SET @sql := IF(@col_exists = 0,
    'ALTER TABLE `AuditFindings` ADD COLUMN `AuditSource` varchar(20) NOT NULL DEFAULT ''internal''',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @col_exists := (SELECT COUNT(*) FROM information_schema.columns
                    WHERE table_schema = DATABASE()
                      AND table_name = 'AuditFindings'
                      AND column_name = 'ExternalAuditId');
SET @sql := IF(@col_exists = 0,
    'ALTER TABLE `AuditFindings` ADD COLUMN `ExternalAuditId` int NULL, ADD INDEX `IX_AuditFindings_ExternalAuditId` (`ExternalAuditId`)',
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
            }
            else
            {
                // SQLite
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""ExternalAuditBodies"" (
    ""Id""          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""Name""        TEXT NOT NULL,
    ""Description"" TEXT NULL,
    ""IsActive""    INTEGER NOT NULL DEFAULT 1,
    ""SortOrder""   INTEGER NOT NULL DEFAULT 0,
    ""CreatedAt""   TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ExternalAuditBodies_Name""     ON ""ExternalAuditBodies""(""Name"");
CREATE INDEX IF NOT EXISTS ""IX_ExternalAuditBodies_IsActive""        ON ""ExternalAuditBodies""(""IsActive"");

CREATE TABLE IF NOT EXISTS ""ExternalAudits"" (
    ""Id""                INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""Code""              TEXT NOT NULL,
    ""AuditingBody""      TEXT NOT NULL,
    ""Standard""          TEXT NULL,
    ""Subject""           TEXT NOT NULL,
    ""AuditDate""         TEXT NOT NULL,
    ""EndDate""           TEXT NULL,
    ""ResponsibleDeptId"" INTEGER NULL,
    ""ResponsibleUserId"" INTEGER NULL,
    ""Status""            TEXT NOT NULL DEFAULT 'planned',
    ""Score""             TEXT NULL,
    ""ReportFilePath""    TEXT NULL,
    ""Notes""             TEXT NULL,
    ""CreatedById""       INTEGER NOT NULL,
    ""CreatedAt""         TEXT NOT NULL,
    CONSTRAINT ""FK_ExternalAudits_Users_CreatedById""
        FOREIGN KEY (""CreatedById"") REFERENCES ""Users""(""Id"") ON DELETE RESTRICT,
    CONSTRAINT ""FK_ExternalAudits_Departments_ResponsibleDeptId""
        FOREIGN KEY (""ResponsibleDeptId"") REFERENCES ""Departments""(""Id"") ON DELETE SET NULL,
    CONSTRAINT ""FK_ExternalAudits_Users_ResponsibleUserId""
        FOREIGN KEY (""ResponsibleUserId"") REFERENCES ""Users""(""Id"") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS ""IX_ExternalAudits_AuditingBody""      ON ""ExternalAudits""(""AuditingBody"");
CREATE INDEX IF NOT EXISTS ""IX_ExternalAudits_Status""            ON ""ExternalAudits""(""Status"");
CREATE INDEX IF NOT EXISTS ""IX_ExternalAudits_AuditDate""         ON ""ExternalAudits""(""AuditDate"");
CREATE INDEX IF NOT EXISTS ""IX_ExternalAudits_CreatedById""       ON ""ExternalAudits""(""CreatedById"");
CREATE INDEX IF NOT EXISTS ""IX_ExternalAudits_ResponsibleDeptId"" ON ""ExternalAudits""(""ResponsibleDeptId"");
CREATE INDEX IF NOT EXISTS ""IX_ExternalAudits_ResponsibleUserId"" ON ""ExternalAudits""(""ResponsibleUserId"");

CREATE TABLE IF NOT EXISTS ""UserExternalAuditBodies"" (
    ""Id""           INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    ""UserId""       INTEGER NOT NULL,
    ""AuditingBody"" TEXT NOT NULL,
    ""CanEdit""      INTEGER NOT NULL DEFAULT 0,
    ""CreatedAt""    TEXT NOT NULL,
    CONSTRAINT ""FK_UserExternalAuditBodies_Users_UserId""
        FOREIGN KEY (""UserId"") REFERENCES ""Users""(""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_UserExternalAuditBodies_UserId_Body""
    ON ""UserExternalAuditBodies""(""UserId"",""AuditingBody"");");

                // SQLite — kolonlar yoksa ekle (migration tek seferlik çalışır)
                migrationBuilder.Sql(@"
ALTER TABLE ""AuditFindings"" ADD COLUMN ""AuditSource"" TEXT NOT NULL DEFAULT 'internal';
ALTER TABLE ""AuditFindings"" ADD COLUMN ""ExternalAuditId"" INTEGER NULL;
CREATE INDEX IF NOT EXISTS ""IX_AuditFindings_ExternalAuditId"" ON ""AuditFindings""(""ExternalAuditId"");");
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
            {
                migrationBuilder.Sql(@"
ALTER TABLE `AuditFindings` DROP COLUMN IF EXISTS `ExternalAuditId`;
ALTER TABLE `AuditFindings` DROP COLUMN IF EXISTS `AuditSource`;
DROP TABLE IF EXISTS `UserExternalAuditBodies`;
DROP TABLE IF EXISTS `ExternalAudits`;
DROP TABLE IF EXISTS `ExternalAuditBodies`;");
            }
            else
            {
                migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_AuditFindings_ExternalAuditId"";
DROP TABLE IF EXISTS ""UserExternalAuditBodies"";
DROP TABLE IF EXISTS ""ExternalAudits"";
DROP TABLE IF EXISTS ""ExternalAuditBodies"";
-- SQLite kolon drop için ALTER TABLE … DROP COLUMN 3.35+ gerekir; pragma yaklaşımı atlandı.
");
            }
        }
    }
}
