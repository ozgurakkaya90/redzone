using ClosedXML.Excel;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// ExportService Excel ve PDF çıktılarının temel doğrulamaları.
/// PDF testi yalnızca "exception fırlatmıyor" garantisi verir;
/// Excel testleri sütun adları ve satır sayısını doğrular.
/// </summary>
public class ExportServiceTests
{
    private static ExportService Svc() => new();

    // ── Yardımcı fabrikalar ──────────────────────────────────────────────────

    private static Risk MakeRisk(string code = "R-001", string status = RiskStatus.Approved) =>
        new()
        {
            Code       = code,
            Title      = "Test Riski",
            Status     = status,
            Category   = "Finansal",
            ProposedAt = DateTime.UtcNow,
        };

    private static AuditFinding MakeFinding(string code = "F-001") =>
        new()
        {
            Code      = code,
            Title     = "Test Bulgusu",
            Category  = "Mali",
            Severity  = "Yüksek",
            Status    = FindingStatus.Open,
            CreatedAt = DateTime.UtcNow,
        };

    private static ActionPlan MakeAction(int riskId = 1) =>
        new()
        {
            RiskId      = riskId,
            Description = "Test aksiyonu",
            Status      = ActionStatus.Planned,
            CreatedAt   = DateTime.UtcNow,
        };

    // ── Risk Excel ───────────────────────────────────────────────────────────

    [Fact]
    public void ExportRisksToExcel_EmptyList_ReturnsValidWorkbook()
    {
        var bytes = Svc().ExportRisksToExcel([]);
        Assert.NotEmpty(bytes);

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();
        // Yalnızca başlık satırı olmalı
        Assert.Equal(1, ws.LastRowUsed()?.RowNumber() ?? 1);
    }

    [Fact]
    public void ExportRisksToExcel_SingleRisk_HasHeaderAndDataRow()
    {
        var bytes = Svc().ExportRisksToExcel([MakeRisk()]);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        Assert.Equal(2, ws.LastRowUsed()!.RowNumber()); // başlık + veri
        // Kod sütunu içe aktarmada güncelleme anahtarı olduğu için başlık açıklayıcıdır.
        Assert.StartsWith("Kod", ws.Cell(1, 1).GetString());
        Assert.StartsWith("Başlık", ws.Cell(1, 2).GetString());
        Assert.Equal("R-001", ws.Cell(2, 1).GetString());
    }

    [Fact]
    public void ExportRisksToExcel_MultipleRisks_RowCountMatches()
    {
        var risks = Enumerable.Range(1, 5).Select(i => MakeRisk($"R-{i:D3}")).ToList();
        var bytes = Svc().ExportRisksToExcel(risks);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        Assert.Equal(6, ws.LastRowUsed()!.RowNumber()); // 1 başlık + 5 veri
    }

    [Fact]
    public void ExportRisksToExcel_RiskWithEvaluations_ScorePopulated()
    {
        var risk = MakeRisk();
        risk.Evaluations.Add(new Evaluation
        {
            EvalType  = EvalType.Initial,
            Score     = 150.0,
            RiskLevel = "Yüksek Risk",
        });

        var bytes = Svc().ExportRisksToExcel([risk]);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        var scoreCell = ws.Cell(2, 22); // Başlangıç Skoru kolonu (düzenlenebilir 14 sütun + bilgi sütunları)
        Assert.Contains("150", scoreCell.GetString());
    }

    // ── Bulgu Excel ──────────────────────────────────────────────────────────

    [Fact]
    public void ExportFindingsToExcel_EmptyList_ReturnsValidWorkbook()
    {
        var bytes = Svc().ExportFindingsToExcel([]);
        Assert.NotEmpty(bytes);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        Assert.Single(wb.Worksheets);
    }

    [Fact]
    public void ExportFindingsToExcel_SingleFinding_HasCorrectHeaders()
    {
        var bytes = Svc().ExportFindingsToExcel([MakeFinding()]);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();

        Assert.Equal("Kod",     ws.Cell(1, 1).GetString());
        Assert.Equal("Başlık",  ws.Cell(1, 2).GetString());
        Assert.Equal("Durum",   ws.Cell(1, 5).GetString());
    }

    // ── Aksiyon Planı Excel ──────────────────────────────────────────────────

    [Fact]
    public void ExportActionPlansToExcel_EmptyList_ReturnsValidWorkbook()
    {
        var bytes = Svc().ExportActionPlansToExcel([]);
        Assert.NotEmpty(bytes);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        Assert.Single(wb.Worksheets);
    }

    // ── Şablon indirme ───────────────────────────────────────────────────────

    [Fact]
    public void GetRiskImportTemplate_ReturnsNonEmptyBytes()
    {
        var bytes = Svc().GetRiskImportTemplate();
        Assert.NotEmpty(bytes);
        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var ws = wb.Worksheets.First();
        // İlk hücre başlık içermeli
        Assert.False(string.IsNullOrEmpty(ws.Cell(1, 1).GetString()));
    }

    [Fact]
    public void GetFindingImportTemplate_ReturnsNonEmptyBytes()
    {
        var bytes = Svc().GetFindingImportTemplate();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void GetAuditActionImportTemplate_ReturnsNonEmptyBytes()
    {
        var bytes = Svc().GetAuditActionImportTemplate();
        Assert.NotEmpty(bytes);
    }

    // ── PDF (smoke test — exception yok) ────────────────────────────────────

    [Fact]
    public void ExportRisksToPdf_EmptyList_ReturnsNonEmptyBytes()
    {
        var bytes = Svc().ExportRisksToPdf([], "Test A.Ş.");
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void ExportRisksToPdf_MultipleRisks_ReturnsNonEmptyBytes()
    {
        var risks = Enumerable.Range(1, 3).Select(i => MakeRisk($"R-{i:D3}")).ToList();
        var bytes = Svc().ExportRisksToPdf(risks);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public void ExportFindingsToPdf_EmptyList_ReturnsNonEmptyBytes()
    {
        var bytes = Svc().ExportFindingsToPdf([]);
        Assert.NotEmpty(bytes);
    }

    // Gömülü Lato fontu + MigraDocCore render zincirinin GERÇEK PDF ürettiğini doğrular
    // (QuestPDF → MigraDocCore geçişinin çalışma-zamanı kanıtı). Türkçe karakter içerir.
    [Fact]
    public void ExportRisksToPdf_ProducesValidPdf_WithTurkishGlyphs()
    {
        var risk = MakeRisk("R-2026-001");
        risk.Title    = "Çalışan güvenliği — İŞ KAZASI riski (Şğıöü)";
        risk.Category = "İş Sağlığı ve Güvenliği";
        risk.Evaluations.Add(new Evaluation { EvalType = EvalType.Initial, Score = 270, RiskLevel = "Yüksek Risk" });

        var bytes = Svc().ExportRisksToPdf([risk], "Örnek Şirket A.Ş.");

        Assert.True(bytes.Length > 500, "PDF beklenenden küçük — render başarısız olabilir.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4)); // geçerli PDF imzası
    }
}
