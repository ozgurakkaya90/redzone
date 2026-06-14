using ClosedXML.Excel;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class ImportServiceTests
{
    // Kontrol içe aktarma kitabı: 1 Risk Kodu, 2 Açıklama, 3 Tür (diğer sütunlar opsiyonel).
    private static Stream BuildControlWorkbook(params (string RiskCode, string Desc)[] rows)
    {
        var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Kontroller");
        ws.Cell(1, 1).Value = "Risk Kodu";
        ws.Cell(1, 2).Value = "Açıklama";
        var r = 2;
        foreach (var (code, desc) in rows)
        {
            ws.Cell(r, 1).Value = code;
            ws.Cell(r, 2).Value = desc;
            ws.Cell(r, 3).Value = "Önleyici";
            r++;
        }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void ImportControls_RespectsScope_RejectsOutOfScopeRisk()
    {
        var db = TestDb.Create();
        var svc = new ImportService(db);

        var inScope  = new Risk { Code = "R-IN-001",  Title = "Erişilebilir risk" };
        var outScope = new Risk { Code = "R-OUT-002", Title = "Başka departmanın riski" };
        db.Risks.AddRange(inScope, outScope);
        db.SaveChanges();

        // Kullanıcı yalnızca R-IN-001'e yetkili; kötü niyetli Excel R-OUT-002'ye de kontrol eklemeye çalışıyor.
        using var stream = BuildControlWorkbook(("R-IN-001", "Geçerli kontrol"), ("R-OUT-002", "Kapsam dışı enjeksiyon"));
        var result = svc.ImportControlsFromExcel(stream, importedById: 1, allowedRiskIds: new HashSet<int> { inScope.Id });

        // Yalnızca kapsam-içi kontrol yazılmalı; kapsam-dışı satır reddedilmeli.
        Assert.Equal(1, result.Imported);
        Assert.Equal(1, db.Controls.Count());
        Assert.Equal(inScope.Id, db.Controls.Single().RiskId);
        Assert.Contains(result.Errors, e => e.Contains("R-OUT-002"));
    }

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
