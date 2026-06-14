using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskAcceptanceFields : Migration
    {
        // Yalnızca kalıntı risk kabulü yönetişim alanları eklenir. (Snapshot'taki diğer
        // sütunlar önceki MySQL migration'larında zaten oluşturuldu; burada tekrar eklenmez —
        // aksi halde prod'da "column already exists" hatası olurdu.)
        // Tipler MySQL (datetime(6)/int) için doğru; SQLite tip-affinity ile de uyumludur.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcceptedById",
                table: "Risks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Risks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptanceReviewDate",
                table: "Risks",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AcceptedById", table: "Risks");
            migrationBuilder.DropColumn(name: "AcceptedAt", table: "Risks");
            migrationBuilder.DropColumn(name: "AcceptanceReviewDate", table: "Risks");
        }
    }
}
