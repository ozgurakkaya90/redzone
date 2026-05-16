using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddSourceLibraryItemIdToRisk : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql("ALTER TABLE `Risks` ADD COLUMN IF NOT EXISTS `SourceLibraryItemId` int NULL;");
            else
                migrationBuilder.Sql(@"ALTER TABLE ""Risks"" ADD COLUMN ""SourceLibraryItemId"" INTEGER NULL;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var isMySql = migrationBuilder.ActiveProvider.Contains("MySql");
            if (isMySql)
                migrationBuilder.Sql("ALTER TABLE `Risks` DROP COLUMN IF EXISTS `SourceLibraryItemId`;");
            else
                migrationBuilder.Sql(@"SELECT 1; -- SQLite: DROP COLUMN manuel yapılmalı");
        }
    }
}
