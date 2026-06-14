using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <summary>
    /// Şema değişikliği YOK — bilinçli olarak boş (no-op) bir baseline migration.
    /// Tek amacı: design-time model snapshot'ını üretim provider'ına (MySQL) hizalamaktır.
    /// Snapshot tarihsel olarak SQLite tipleriyle (TEXT/INTEGER) tutulmuştu; bu yüzden her
    /// yeni migration üretiminde sahte AlterColumn (TEXT→varchar) kirliliği oluşuyordu.
    /// Bu migration ile snapshot MySQL tiplerine (varchar/longtext/datetime(6)/int) geçirildi;
    /// bundan sonra üretilen migration'lar yalnızca gerçek değişiklikleri içerir.
    /// Üretimde uygulanması güvenlidir (hiçbir DDL çalıştırmaz).
    /// </summary>
    public partial class AlignSnapshotToMySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bilinçli olarak boş — yalnızca snapshot hizalaması.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Bilinçli olarak boş.
        }
    }
}
