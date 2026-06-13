using ClosedXML.Excel;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class ImportServiceTests
{
    // Yalnızca Kod (boş = yeni) ve Başlık sütunlarını dolduran asgari bir risk çalışma kitabı.
    private static Stream BuildRiskWorkbook(params string[] titles)
    {
        var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Riskler");
        ws.Cell(1, 1).Value = "Kod";      // başlık satırı (import Skip(1) ile atlar)
        ws.Cell(1, 2).Value = "Başlık";

        var row = 2;
        foreach (var t in titles)
        {
            ws.Cell(row, 2).Value = t;    // Kod boş bırakılır → yeni kayıt; Başlık zorunlu
            row++;
        }

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void ImportRisks_MultipleNewRows_AllPersisted_NoSilentDataLoss()
    {
        var db  = TestDb.Create();
        var svc = new ImportService(db);

        using var stream = BuildRiskWorkbook("Risk A", "Risk B", "Risk C");
        var result = svc.ImportRisksFromExcel(stream, importedById: 1);

        // Regresyon: CounterHelper.GetNext döngü-içinde ChangeTracker.Clear() çağırdığından,
        // her yeni satır bir öncekini context'ten koparıyor ve yalnızca SON satır
        // kaydediliyordu — üstelik result.Imported yanlışlıkla tam sayıyı raporluyordu.
        Assert.Empty(result.Errors);
        Assert.Equal(3, result.Imported);
        Assert.Equal(3, db.Risks.Count());                                  // asıl güvence: DB'de gerçekten 3 kayıt
        Assert.Equal(3, db.Risks.Select(r => r.Code).Distinct().Count());   // kodlar benzersiz üretilmiş
    }
}
