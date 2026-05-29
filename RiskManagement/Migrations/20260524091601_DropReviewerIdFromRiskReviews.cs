using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class DropReviewerIdFromRiskReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `RiskReviews` DROP FOREIGN KEY `FK_RiskReviews_Users`;");
            migrationBuilder.DropColumn(name: "ReviewerId", table: "RiskReviews");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "ReviewerId", table: "RiskReviews", nullable: false, defaultValue: 0);
        }
    }
}
