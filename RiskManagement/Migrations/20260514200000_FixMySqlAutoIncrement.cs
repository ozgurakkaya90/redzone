using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(RiskManagement.Data.AppDbContext))]
    [Migration("20260514200000_FixMySqlAutoIncrement")]
    public partial class FixMySqlAutoIncrement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!migrationBuilder.ActiveProvider.Contains("MySql"))
                return;

            // ── Adım 1: Tablolar yoksa oluştur (IF NOT EXISTS ile güvenli) ─────────
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `AuditPlans` (
    `Id`          int NOT NULL AUTO_INCREMENT,
    `Year`        int NOT NULL DEFAULT 0,
    `Title`       varchar(200) NOT NULL DEFAULT '',
    `Description` longtext NULL,
    `CreatedById` int NOT NULL DEFAULT 0,
    `CreatedAt`   datetime(6) NOT NULL DEFAULT '2000-01-01 00:00:00',
    PRIMARY KEY (`Id`)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `AuditPlanItems` (
    `Id`               int NOT NULL AUTO_INCREMENT,
    `AuditPlanId`      int NOT NULL DEFAULT 0,
    `Title`            varchar(200) NOT NULL DEFAULT '',
    `AuditType`        varchar(100) NULL,
    `AuditedUnit`      varchar(200) NULL,
    `Description`      varchar(500) NULL,
    `PlannedStartDate` date NOT NULL DEFAULT '2000-01-01',
    `PlannedEndDate`   date NOT NULL DEFAULT '2000-01-01',
    `ActualStartDate`  date NULL,
    `ActualEndDate`    date NULL,
    `DepartmentId`     int NULL,
    `ResponsibleId`    int NULL,
    `Notes`            varchar(500) NULL,
    `SortOrder`        int NOT NULL DEFAULT 0,
    PRIMARY KEY (`Id`)
) CHARACTER SET utf8mb4;");

            // ── Adım 2: Id AUTO_INCREMENT ekle (tablo varsa kolonunu düzelt) ────────
            // MODIFY sadece değişiklik gerektiriyorsa etki eder, aksi halde no-op'tur.
            migrationBuilder.Sql("ALTER TABLE `AuditPlans`    MODIFY COLUMN `Id` int NOT NULL AUTO_INCREMENT;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` MODIFY COLUMN `Id` int NOT NULL AUTO_INCREMENT;");

            // ── Adım 3: Eksik kolonlar — IF NOT EXISTS (MySQL 8.0.29+) ──────────────
            migrationBuilder.Sql("ALTER TABLE `AuditPlans` ADD COLUMN IF NOT EXISTS `Year`        int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlans` ADD COLUMN IF NOT EXISTS `Title`       varchar(200) NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE `AuditPlans` ADD COLUMN IF NOT EXISTS `Description` longtext NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlans` ADD COLUMN IF NOT EXISTS `CreatedById` int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlans` ADD COLUMN IF NOT EXISTS `CreatedAt`   datetime(6) NOT NULL DEFAULT '2000-01-01 00:00:00';");

            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `AuditPlanId`      int NOT NULL DEFAULT 0;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `Title`            varchar(200) NOT NULL DEFAULT '';");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `AuditType`        varchar(100) NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `AuditedUnit`      varchar(200) NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `Description`      varchar(500) NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `PlannedStartDate` date NOT NULL DEFAULT '2000-01-01';");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `PlannedEndDate`   date NOT NULL DEFAULT '2000-01-01';");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `ActualStartDate`  date NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `ActualEndDate`    date NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `DepartmentId`     int NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `ResponsibleId`    int NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `Notes`            varchar(500) NULL;");
            migrationBuilder.Sql("ALTER TABLE `AuditPlanItems` ADD COLUMN IF NOT EXISTS `SortOrder`        int NOT NULL DEFAULT 0;");

            migrationBuilder.Sql("ALTER TABLE `InternalAudits` ADD COLUMN IF NOT EXISTS `DepartmentId`    int NULL;");
            migrationBuilder.Sql("ALTER TABLE `InternalAudits` ADD COLUMN IF NOT EXISTS `AuditPlanItemId` int NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Onarım migration'ı — geri alınamaz
        }
    }
}
