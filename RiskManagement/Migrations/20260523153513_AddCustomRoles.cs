using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS `CustomRoles`;
                CREATE TABLE `CustomRoles` (
                    `Id`        int NOT NULL AUTO_INCREMENT,
                    `Name`      varchar(100) NOT NULL,
                    `Label`     varchar(200) NOT NULL,
                    `IsSystem`  tinyint(1) NOT NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    CONSTRAINT `PK_CustomRoles` PRIMARY KEY (`Id`)
                );
                CREATE UNIQUE INDEX `IX_CustomRoles_Name` ON `CustomRoles` (`Name`);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomRoles");
        }
    }
}
