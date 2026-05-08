using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddMultiRoleDeptOrgCompany : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS kullanarak idempotent — migration kısmen çalışmış olsa bile güvenli
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");

            if (isMySql)
            {
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `UserRoles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `RoleName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_UserRoles_UserId_RoleName` (`UserId`, `RoleName`),
    CONSTRAINT `FK_UserRoles_Users_UserId` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;");

                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `UserDepartments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `DepartmentId` int NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_UserDepartments_UserId_DeptId` (`UserId`, `DepartmentId`),
    CONSTRAINT `FK_UserDepts_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserDepts_Depts` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;");

                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `UserOrganizations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `OrganizationId` int NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_UserOrgs_UserId_OrgId` (`UserId`, `OrganizationId`),
    CONSTRAINT `FK_UserOrgs_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserOrgs_Orgs` FOREIGN KEY (`OrganizationId`) REFERENCES `Organizations` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;");

                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `UserCompanies` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` int NOT NULL,
    `CompanyId` int NOT NULL,
    PRIMARY KEY (`Id`),
    UNIQUE INDEX `IX_UserCompanies_UserId_CompanyId` (`UserId`, `CompanyId`),
    CONSTRAINT `FK_UserCompanies_Users` FOREIGN KEY (`UserId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_UserCompanies_Companies` FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;");
            }
            else
            {
                // SQLite
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""UserRoles"" (
    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
    ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
    ""RoleName"" TEXT NOT NULL,
    UNIQUE(""UserId"", ""RoleName"")
);");
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""UserDepartments"" (
    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
    ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
    ""DepartmentId"" INTEGER NOT NULL REFERENCES ""Departments""(""Id"") ON DELETE CASCADE,
    UNIQUE(""UserId"", ""DepartmentId"")
);");
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""UserOrganizations"" (
    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
    ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
    ""OrganizationId"" INTEGER NOT NULL REFERENCES ""Organizations""(""Id"") ON DELETE CASCADE,
    UNIQUE(""UserId"", ""OrganizationId"")
);");
                migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""UserCompanies"" (
    ""Id"" INTEGER PRIMARY KEY AUTOINCREMENT,
    ""UserId"" INTEGER NOT NULL REFERENCES ""Users""(""Id"") ON DELETE CASCADE,
    ""CompanyId"" INTEGER NOT NULL REFERENCES ""Companies""(""Id"") ON DELETE CASCADE,
    UNIQUE(""UserId"", ""CompanyId"")
);");
            }

            // Veri senkronizasyonu Program.cs'de yapılıyor (SyncUserAssignments)
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS UserRoles;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS UserDepartments;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS UserOrganizations;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS UserCompanies;");
        }
    }
}
