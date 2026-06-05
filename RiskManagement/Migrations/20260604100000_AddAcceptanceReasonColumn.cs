using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    public partial class AddAcceptanceReasonColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceReason",
                table: "Risks",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Mevcut risk_accepted kayıtlarının RejectionReason'ını AcceptanceReason'a taşı
            migrationBuilder.Sql(
                "UPDATE Risks SET AcceptanceReason = RejectionReason, RejectionReason = NULL " +
                "WHERE Status = 'risk_accepted' AND RejectionReason IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Geri alırken AcceptanceReason'ı RejectionReason'a geri taşı
            migrationBuilder.Sql(
                "UPDATE Risks SET RejectionReason = AcceptanceReason " +
                "WHERE Status = 'risk_accepted' AND AcceptanceReason IS NOT NULL AND RejectionReason IS NULL");

            migrationBuilder.DropColumn(
                name: "AcceptanceReason",
                table: "Risks");
        }
    }
}
