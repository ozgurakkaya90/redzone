using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RiskManagement.Data;

#nullable disable

namespace RiskManagement.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260521000001_MergeDraftingStatus")]
    public partial class MergeDraftingStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `Risks` SET `Status` = 'under_review' WHERE `Status` = 'drafting'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Veri dönüşümü geri alınamaz — hangi kayıtların "drafting" olduğu bilinmiyor.
        }
    }
}
