using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiskManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bu migration kasıtlı olarak boştur.
            //
            // Neden: Risk.Status, Risk.Code, AuditFinding.Status, AuditFinding.Code
            // gibi sütunlar MySQL'de [MaxLength] olmadığı için longtext olarak eşleniyor.
            // MySQL, longtext sütunlara prefix belirtmeden index ekleyemiyor
            // (BLOB/TEXT column used in key specification without a key length).
            //
            // Compound index (RiskAuditLogs.RiskId + Timestamp) ise FK kısıtlaması
            // nedeniyle doğrudan DROP INDEX yapılamıyordu.
            //
            // Doğru çözüm: string sütunlara [MaxLength] ekle → yeni migration oluştur.
            // Bu migration sadece __EFMigrationsHistory'e kaydedilmek için çalışıyor.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op — Up() hiçbir şey yapmadığı için geri alınacak bir şey yok.
        }
    }
}
