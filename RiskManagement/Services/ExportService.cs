using ClosedXML.Excel;
using MigraDocCore.DocumentObjectModel;
using MigraDocCore.DocumentObjectModel.Tables;
using MigraDocCore.Rendering;
using PdfSharpCore.Fonts;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class ExportService
{
    // ── PDF altyapısı (MigraDoc + PdfSharpCore, MIT) ──────────────────────────
    private static readonly object _fontLock = new();
    private static bool _fontReady;
    private static readonly Color Navy  = new(30, 58, 95);    // #1E3A5F
    private static readonly Color AltBg = new(248, 250, 252); // #F8FAFC
    private static readonly Color Muted = new(100, 116, 139); // #64748B

    private static void EnsureFonts()
    {
        if (_fontReady) return;
        lock (_fontLock)
        {
            if (_fontReady) return;
            // Gömülü Lato resolver'ı global olarak bir kez ayarla (idempotent).
            try { GlobalFontSettings.FontResolver = LatoFontResolver.Instance; }
            catch { /* zaten ayarlıysa yut */ }
            _fontReady = true;
        }
    }

    /// <summary>
    /// Tüm liste-PDF raporları için ortak şablon: başlık + şirket/tarih + sayım + renkli
    /// başlıklı tablo + sayfa numaralı altbilgi. A4 yatay.
    /// </summary>
    private static byte[] BuildTablePdf(string title, string countLabel, string companyName,
        (string Label, double Weight)[] columns, IReadOnlyList<string?[]> rows)
    {
        EnsureFonts();

        var doc = new Document();
        var normal = doc.Styles["Normal"];
        normal.Font.Name = "Lato";
        normal.Font.Size = 9;

        var sec = doc.AddSection();
        sec.PageSetup.Orientation = Orientation.Landscape;
        sec.PageSetup.PageFormat  = PageFormat.A4;
        sec.PageSetup.TopMargin = sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(1.3);

        // ── Başlık satırı ──
        var head = sec.AddParagraph();
        head.AddFormattedText(title, new Font { Size = 14, Bold = true, Color = Navy });
        if (!string.IsNullOrEmpty(companyName))
        {
            head.AddTab();
            head.AddFormattedText(companyName, new Font { Size = 9, Color = Muted });
        }
        var dateP = sec.AddParagraph();
        dateP.AddFormattedText(
            DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR")),
            new Font { Size = 9, Color = Muted });
        dateP.AddFormattedText("   ·   " + countLabel, new Font { Size = 9, Color = Muted });
        sec.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);

        // ── Tablo ──
        var table = sec.AddTable();
        table.Borders.Color = new Color(226, 232, 240); // #E2E8F0
        table.Borders.Width = 0.25;

        const double availableCm = 26.5; // A4 yatay - kenar boşlukları
        var totalWeight = columns.Sum(c => c.Weight);
        foreach (var c in columns)
            table.AddColumn(Unit.FromCentimeter(c.Weight / totalWeight * availableCm));

        var header = table.AddRow();
        header.Shading.Color = Navy;
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.White;
        header.Format.Font.Size = 8;
        for (var i = 0; i < columns.Length; i++)
        {
            var p = header.Cells[i].AddParagraph(columns[i].Label);
            p.Format.Font.Bold = true;
            header.Cells[i].VerticalAlignment = VerticalAlignment.Center;
            header.Cells[i].Format.Font.Color = Colors.White;
        }

        var alt = false;
        foreach (var row in rows)
        {
            var r = table.AddRow();
            r.Format.Font.Size = 8;
            if (alt) r.Shading.Color = AltBg;
            alt = !alt;
            for (var i = 0; i < columns.Length && i < row.Length; i++)
                r.Cells[i].AddParagraph(row[i] ?? "—");
        }

        // ── Altbilgi: sayfa no ──
        var footer = sec.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = new Color(148, 163, 184); // #94A3B8
        footer.AddText("Sayfa ");
        footer.AddPageField();
        footer.AddText(" / ");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms);
        return ms.ToArray();
    }

    // ── Excel ────────────────────────────────────────────────────────────────

    // Risk envanteri sütun düzeni — Şablon, İçe Aktarma ve Dışa Aktarma'da aynı sırayla
    // kullanılır. İlk 14 sütun düzenlenebilir/içe aktarılabilir; 15+ salt-okunur bilgi sütunlarıdır.
    // Böylece "dışa aktar → düzenle → tekrar içe aktar" (Kod ile eşleşme) sorunsuz çalışır.
    internal static readonly string[] RiskEditableHeaders =
    [
        "Kod (boşsa yeni kayıt)", "Başlık*", "Açıklama", "Kategori",
        "Kaynak Sınıflandırması (İç Faktör/Dış Faktör)", "Kaynak Türü", "Tehlike", "Olası Etki",
        "Faaliyet Alanı", "Etkilenecek Kişiler (virgülle ayırın)", "İlgili Mevzuat / Doküman",
        "Risk Stratejisi", "Mevcut Durum", "Aktif/Pasif"
    ];

    private static readonly string[] RiskReadonlyHeaders =
    [
        "İş Akışı Durumu", "Öneren", "Sorumlu", "Departman", "Organizasyon",
        "Önerilme Tarihi", "Son Değerlendirme",
        "Başlangıç Skoru", "Başlangıç Seviyesi", "Artık Skor", "Artık Seviye"
    ];

    public byte[] ExportRisksToExcel(IEnumerable<Risk> risks)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Risk Kaydı");

        var headers = RiskEditableHeaders.Concat(RiskReadonlyHeaders).ToArray();
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }
        // Salt-okunur sütunları görsel olarak ayırt et
        for (int i = RiskEditableHeaders.Length; i < headers.Length; i++)
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#64748B");

        int row = 2;
        foreach (var r in risks)
        {
            var initial  = r.Evaluations.LastOrDefault(e => e.EvalType == "initial");
            var residual = r.Evaluations.LastOrDefault(e => e.EvalType == "residual");

            // Düzenlenebilir sütunlar (1-14)
            ws.Cell(row, 1).Value  = SafeCell(r.Code);
            ws.Cell(row, 2).Value  = SafeCell(r.Title);
            ws.Cell(row, 3).Value  = SafeCell(r.Description);
            ws.Cell(row, 4).Value  = SafeCell(r.Category);
            ws.Cell(row, 5).Value  = r.SourceType == "external" ? "Dış Faktör" : "İç Faktör";
            ws.Cell(row, 6).Value  = SafeCell(r.Source);
            ws.Cell(row, 7).Value  = SafeCell(r.Hazard);
            ws.Cell(row, 8).Value  = SafeCell(r.PossibleImpact);
            ws.Cell(row, 9).Value  = SafeCell(r.ActivityArea);
            ws.Cell(row, 10).Value = SafeCell(string.Join(", ", r.GetAffectedPersonsList()));
            ws.Cell(row, 11).Value = SafeCell(r.RelevantLegislation);
            ws.Cell(row, 12).Value = SafeCell(r.RiskStrategy);
            ws.Cell(row, 13).Value = SafeCell(r.CurrentStatus);
            ws.Cell(row, 14).Value = r.IsActive ? "Aktif" : "Pasif";
            // Salt-okunur bilgi sütunları (15+)
            ws.Cell(row, 15).Value = StatusLabel(r.Status);
            ws.Cell(row, 16).Value = SafeCell(r.ProposedBy?.FullName ?? r.ProposerName);
            ws.Cell(row, 17).Value = SafeCell(r.Owner?.FullName);
            ws.Cell(row, 18).Value = SafeCell(r.Department?.Name);
            ws.Cell(row, 19).Value = SafeCell(r.Organization?.Name);
            ws.Cell(row, 20).Value = r.ProposedAt.ToString("dd.MM.yyyy");
            ws.Cell(row, 21).Value = r.LastReviewedAt?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 22).Value = initial?.Score.ToString("F1") ?? "";
            ws.Cell(row, 23).Value = SafeCell(initial?.RiskLevel);
            ws.Cell(row, 24).Value = residual?.Score.ToString("F1") ?? "";
            ws.Cell(row, 25).Value = SafeCell(residual?.RiskLevel);
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportFindingsToExcel(IEnumerable<AuditFinding> findings)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Denetim Bulguları");

        string[] headers =
        [
            "Kod", "Başlık", "Kategori", "Ciddiyet", "Durum",
            "Denetçi", "Bulgu Sahibi", "Departman", "Bitiş Tarihi",
            "Oluşturulma", "Kapanma", "Denetim"
        ];

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var f in findings)
        {
            ws.Cell(row, 1).Value  = SafeCell(f.Code);
            ws.Cell(row, 2).Value  = SafeCell(f.Title);
            ws.Cell(row, 3).Value  = SafeCell(f.Category);
            ws.Cell(row, 4).Value  = SafeCell(f.Severity);
            ws.Cell(row, 5).Value  = FindingStatusLabel(f.Status);
            ws.Cell(row, 6).Value  = SafeCell(f.Auditor?.FullName);
            ws.Cell(row, 7).Value  = SafeCell(f.Owner?.FullName);
            ws.Cell(row, 8).Value  = SafeCell(f.Department?.Name);
            ws.Cell(row, 9).Value  = f.DueDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 10).Value = f.CreatedAt.ToString("dd.MM.yyyy");
            ws.Cell(row, 11).Value = f.ClosedAt?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 12).Value = SafeCell(f.InternalAudit?.Title);
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── PDF ──────────────────────────────────────────────────────────────────

    public byte[] ExportRisksToPdf(IEnumerable<Risk> risks, string companyName = "")
    {
        var list = risks.ToList();
        var rows = list.Select(r =>
        {
            var initial = r.Evaluations.FirstOrDefault(e => e.EvalType == "initial");
            return new string?[]
            {
                r.Code, r.Title, r.Category, StatusLabel(r.Status),
                initial?.Score.ToString("F1"), initial?.RiskLevel,
                r.Owner?.FullName, r.Department?.Name,
            };
        }).ToList();

        return BuildTablePdf("RİSK KAYDI RAPORU", $"Toplam {list.Count} risk kaydı", companyName,
            new[] { ("Kod", 1.2), ("Başlık", 3.0), ("Kategori", 1.5), ("Durum", 1.5),
                    ("Skor", 1.0), ("Seviye", 1.5), ("Sorumlu", 2.0), ("Departman", 1.5) },
            rows);
    }

    public byte[] ExportFindingsToPdf(IEnumerable<AuditFinding> findings, string companyName = "")
    {
        var list = findings.ToList();
        var rows = list.Select(f => new string?[]
        {
            f.Code, f.Title, f.Severity, FindingStatusLabel(f.Status),
            f.Auditor?.FullName, f.Owner?.FullName, f.Department?.Name,
            f.DueDate?.ToString("dd.MM.yyyy"),
        }).ToList();

        return BuildTablePdf("İÇ DENETİM BULGULARI RAPORU", $"Toplam {list.Count} bulgu", companyName,
            new[] { ("Kod", 1.2), ("Başlık", 3.0), ("Ciddiyet", 1.5), ("Durum", 1.5),
                    ("Denetçi", 2.0), ("Bulgu Sahibi", 2.0), ("Departman", 1.5), ("Bitiş Tarihi", 1.3) },
            rows);
    }

    // ── Kontrol Planı ─────────────────────────────────────────────────────────

    public byte[] ExportControlsToExcel(IEnumerable<Control> controls)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Kontrol Planı");

        string[] headers = ["Risk Kodu", "Risk Başlığı", "Kontrol Açıklaması", "Tür", "Etkinlik", "Sıklık", "Sahibi Departman", "Giren", "Tarih"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var c in controls)
        {
            ws.Cell(row, 1).Value = SafeCell(c.Risk?.Code);
            ws.Cell(row, 2).Value = SafeCell(c.Risk?.Title);
            ws.Cell(row, 3).Value = SafeCell(c.Description);
            ws.Cell(row, 4).Value = SafeCell(c.ControlType);
            ws.Cell(row, 5).Value = SafeCell(c.Effectiveness);
            ws.Cell(row, 6).Value = SafeCell(c.Frequency);
            ws.Cell(row, 7).Value = SafeCell(c.OwnerDept?.Name);
            ws.Cell(row, 8).Value = SafeCell(c.EnteredBy?.FullName);
            ws.Cell(row, 9).Value = c.EnteredAt.ToString("dd.MM.yyyy");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportControlsToPdf(IEnumerable<Control> controls, string companyName = "")
    {
        var list = controls.ToList();
        var rows = list.Select(c => new string?[]
        {
            c.Risk?.Code, c.Description, c.ControlType, c.Effectiveness, c.Frequency, c.OwnerDept?.Name,
        }).ToList();

        return BuildTablePdf("KONTROL PLANI RAPORU", $"Toplam {list.Count} kontrol kaydı", companyName,
            new[] { ("Risk Kodu", 1.2), ("Kontrol Açıklaması", 3.0), ("Tür", 1.5),
                    ("Etkinlik", 1.5), ("Sıklık", 1.5), ("Sahibi", 2.0) },
            rows);
    }

    // ── Risk Aksiyon Planları ─────────────────────────────────────────────────

    public byte[] ExportActionPlansToExcel(IEnumerable<ActionPlan> plans)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Aksiyon Planları");

        string[] headers = ["Risk Kodu", "Risk Başlığı", "Aksiyon Açıklaması", "Sorumlu", "Sahibi Departman", "Hedef Tarih", "Durum", "Oluşturan", "Oluşturulma"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var a in plans)
        {
            ws.Cell(row, 1).Value = SafeCell(a.Risk?.Code);
            ws.Cell(row, 2).Value = SafeCell(a.Risk?.Title);
            ws.Cell(row, 3).Value = SafeCell(a.Description);
            ws.Cell(row, 4).Value = SafeCell(a.Responsible);
            ws.Cell(row, 5).Value = SafeCell(a.OwnerDept?.Name);
            ws.Cell(row, 6).Value = a.DueDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 7).Value = ActionStatusLabel(a.Status);
            ws.Cell(row, 8).Value = SafeCell(a.CreatedBy?.FullName);
            ws.Cell(row, 9).Value = a.CreatedAt.ToString("dd.MM.yyyy");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportActionPlansToPdf(IEnumerable<ActionPlan> plans, string companyName = "")
    {
        var list = plans.ToList();
        var rows = list.Select(a => new string?[]
        {
            a.Risk?.Code, a.Description, a.Responsible, a.OwnerDept?.Name,
            a.DueDate?.ToString("dd.MM.yyyy"), ActionStatusLabel(a.Status),
        }).ToList();

        return BuildTablePdf("RİSK AKSİYON PLANLARI RAPORU", $"Toplam {list.Count} aksiyon kaydı", companyName,
            new[] { ("Risk Kodu", 1.2), ("Aksiyon", 3.0), ("Sorumlu", 2.0),
                    ("Departman", 1.5), ("Hedef Tarih", 1.3), ("Durum", 1.5) },
            rows);
    }

    // ── Denetim Aksiyon Planları ──────────────────────────────────────────────

    public byte[] ExportAuditActionsToExcel(IEnumerable<AuditFindingAction> actions)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Denetim Aksiyonları");

        string[] headers = ["Bulgu Kodu", "Bulgu Başlığı", "Aksiyon Açıklaması", "Sorumlu", "Hedef Tarih", "Durum", "Oluşturan", "Oluşturulma"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var a in actions)
        {
            ws.Cell(row, 1).Value = SafeCell(a.Finding?.Code);
            ws.Cell(row, 2).Value = SafeCell(a.Finding?.Title);
            ws.Cell(row, 3).Value = SafeCell(a.Description);
            ws.Cell(row, 4).Value = SafeCell(a.Responsible);
            ws.Cell(row, 5).Value = a.DueDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 6).Value = ActionStatusLabel(a.Status);
            ws.Cell(row, 7).Value = SafeCell(a.CreatedBy?.FullName);
            ws.Cell(row, 8).Value = a.CreatedAt.ToString("dd.MM.yyyy");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportAuditActionsToPdf(IEnumerable<AuditFindingAction> actions, string companyName = "")
    {
        var list = actions.ToList();
        var rows = list.Select(a => new string?[]
        {
            a.Finding?.Code, a.Description, a.Responsible,
            a.DueDate?.ToString("dd.MM.yyyy"), ActionStatusLabel(a.Status),
        }).ToList();

        return BuildTablePdf("DENETİM AKSİYON PLANLARI RAPORU", $"Toplam {list.Count} aksiyon kaydı", companyName,
            new[] { ("Bulgu Kodu", 1.2), ("Aksiyon", 3.0), ("Sorumlu", 2.0),
                    ("Hedef Tarih", 1.3), ("Durum", 1.5) },
            rows);
    }

    // ── Etik Bildirimler ──────────────────────────────────────────────────────

    public byte[] ExportEthicsToExcel(IEnumerable<EthicsReport> reports)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Etik Bildirimler");

        string[] headers = ["Kod", "Konu", "Kategori", "Durum", "Bildirim Tarihi", "Denetim Kararı", "Kurul Kararı"];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var r in reports)
        {
            ws.Cell(row, 1).Value = SafeCell(r.Code);
            ws.Cell(row, 2).Value = SafeCell(r.Subject);
            ws.Cell(row, 3).Value = SafeCell(r.ReportCategory);
            ws.Cell(row, 4).Value = EthicsStatusLabel(r.Status);
            ws.Cell(row, 5).Value = r.SubmittedAt.ToString("dd.MM.yyyy");
            ws.Cell(row, 6).Value = SafeCell(r.AuditDecision);
            ws.Cell(row, 7).Value = SafeCell(r.EthicsDecision);
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportEthicsToPdf(IEnumerable<EthicsReport> reports, string companyName = "")
    {
        var list = reports.ToList();
        var rows = list.Select(r => new string?[]
        {
            r.Code, r.Subject, r.ReportCategory, EthicsStatusLabel(r.Status),
            r.SubmittedAt.ToString("dd.MM.yyyy"),
        }).ToList();

        return BuildTablePdf("ETİK BİLDİRİMLER RAPORU", $"Toplam {list.Count} etik bildirim", companyName,
            new[] { ("Kod", 1.2), ("Konu", 3.0), ("Kategori", 1.5), ("Durum", 1.5), ("Tarih", 1.3) },
            rows);
    }

    // ── Dış Denetim Aksiyon Planları ─────────────────────────────────────────

    public byte[] ExportExternalAuditActionsToExcel(IEnumerable<AuditFindingAction> actions)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Dış Denetim Aksiyonları");

        string[] headers =
        [
            "Kurum", "Denetim Kodu", "Denetim Konusu",
            "Bulgu Kodu", "Bulgu Başlığı", "Majör/Minör",
            "Aksiyon Açıklaması", "Sorumlu Birim", "Termin", "Durum",
            "Durum Notu", "Oluşturulma"
        ];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var a in actions)
        {
            var f  = a.Finding;
            var ea = f?.ExternalAudit;
            ws.Cell(row, 1).Value  = SafeCell(ea?.AuditingBody);
            ws.Cell(row, 2).Value  = SafeCell(ea?.Code);
            ws.Cell(row, 3).Value  = SafeCell(ea?.Subject);
            ws.Cell(row, 4).Value  = SafeCell(f?.Code);
            ws.Cell(row, 5).Value  = SafeCell(f?.Title);
            ws.Cell(row, 6).Value  = SafeCell(f?.Severity);
            ws.Cell(row, 7).Value  = SafeCell(a.Description);
            ws.Cell(row, 8).Value  = SafeCell(a.Responsible);
            ws.Cell(row, 9).Value  = a.DueDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 10).Value = ActionStatusLabel(a.Status);
            ws.Cell(row, 11).Value = SafeCell(a.ClosureNote);
            ws.Cell(row, 12).Value = a.CreatedAt.ToString("dd.MM.yyyy");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Dış Denetim Uygunsuzluk Aksiyon Planı ────────────────────────────────

    // 15 sütunluk resmi format — import şablonuyla birebir eşleşir
    private static readonly string[] NonconformityHeaders =
    [
        "Geçirilen Denetim Adı", "Denetim Tarihi", "Denetim Türü",
        "İlgili Mevzuat/Rehber/Standart", "İlgili Mevzuat Maddesi (Varsa)",
        "İlgili Denetim Listesi Adı (Varsa)", "Uygunsuzluk Tespit Edildi mi? (Evet/Hayır)",
        "Uygunsuzluk Adedi", "Uygunsuzluğa Konu Olan Standart Maddesi (Varsa)",
        "Uygunsuzluk Detay Açıklama", "Majör/Minör",
        "Uygunsuzluğu Gidermek İçin Alınan Aksiyon",
        "Sorumlu Departman", "Termin", "Durum"
    ];

    public byte[] ExportExternalAuditNonconformitiesToExcel(
        IEnumerable<AuditFinding> findings)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Uygunsuzluk Aksiyon Planı");

        for (int i = 0; i < NonconformityHeaders.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = NonconformityHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }

        int row = 2;
        foreach (var f in findings)
        {
            var audit = f.ExternalAudit;
            var action = f.Actions.OrderByDescending(a => a.CreatedAt).FirstOrDefault();

            ws.Cell(row, 1).Value  = SafeCell(audit?.Subject ?? f.InternalAudit?.Title);
            ws.Cell(row, 2).Value  = audit?.AuditDate.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 3).Value  = SafeCell(audit?.AuditType);
            ws.Cell(row, 4).Value  = SafeCell(audit?.Standard ?? audit?.AuditingBody);
            ws.Cell(row, 5).Value  = SafeCell(f.StandardArticle);
            ws.Cell(row, 6).Value  = SafeCell(audit?.ChecklistName);
            ws.Cell(row, 7).Value  = "Evet";
            ws.Cell(row, 8).Value  = f.NonconformityCount.HasValue ? f.NonconformityCount.Value.ToString() : "";
            ws.Cell(row, 9).Value  = SafeCell(f.StandardClause);
            ws.Cell(row, 10).Value = SafeCell(f.Description ?? f.Title);
            ws.Cell(row, 11).Value = SafeCell(f.Severity);
            ws.Cell(row, 12).Value = SafeCell(action?.Description);
            ws.Cell(row, 13).Value = SafeCell(action?.Responsible);
            ws.Cell(row, 14).Value = (action?.DueDate ?? f.DueDate)?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 15).Value = NonconformityStatusLabel(f.Status, action?.Status);
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetExternalAuditNonconformityTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Uygunsuzluk Şablonu");
        AddTemplateHeaders(ws, NonconformityHeaders);
        // İkinci örnek satır (salt rehber)
        ws.Cell(3, 1).Value  = "JCI Denetimi";
        ws.Cell(3, 2).Value  = "25.01.2014";
        ws.Cell(3, 3).Value  = "Uluslararası Akreditasyon";
        ws.Cell(3, 4).Value  = "JCI";
        ws.Cell(3, 5).Value  = "IPSG.1";
        ws.Cell(3, 6).Value  = "JCI 2014 Nihai Karar Raporu";
        ws.Cell(3, 7).Value  = "Evet";
        ws.Cell(3, 8).Value  = "1";
        ws.Cell(3, 9).Value  = "Hasta kimlik doğrulama maddesi";
        ws.Cell(3, 10).Value = "Hasta barkodlarındaki bilgiler politikadan fazla...";
        ws.Cell(3, 11).Value = "Minör";
        ws.Cell(3, 12).Value = "Barkot puntosu büyütüldü, renk standardize edildi.";
        ws.Cell(3, 13).Value = "Hasta Hizmetleri";
        ws.Cell(3, 14).Value = "31.03.2014";
        ws.Cell(3, 15).Value = "Tamamlandı";
        for (int c = 1; c <= NonconformityHeaders.Length; c++)
        {
            var cell = ws.Cell(3, c);
            cell.Style.Font.Italic = true;
            cell.Style.Font.FontColor = XLColor.Gray;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── İçe Aktarma Şablonları ───────────────────────────────────────────────

    public byte[] GetRiskImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Risk Şablonu");
        AddTemplateHeaders(ws, RiskEditableHeaders);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetControlImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Kontrol Şablonu");
        string[] headers = ["Risk Kodu*", "Kontrol Açıklaması*", "Tür (Önleyici/Tespit Edici/Düzeltici)", "Etkinlik", "Sıklık", "Sahibi Departman"];
        AddTemplateHeaders(ws, headers);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetActionPlanImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Aksiyon Şablonu");
        string[] headers = ["Risk Kodu*", "Aksiyon Açıklaması*", "Sorumlu*", "Sahibi Departman", "Hedef Tarih (gg.AA.yyyy)", "Durum (Planlandı/Devam Ediyor/Tamamlandı/İptal)"];
        AddTemplateHeaders(ws, headers);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetFindingImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Bulgu Şablonu");
        string[] headers = ["Başlık*", "Açıklama", "Kategori", "Şiddet (Kritik/Yüksek/Orta/Düşük)", "Bitiş Tarihi (gg.AA.yyyy)"];
        AddTemplateHeaders(ws, headers);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetAuditActionImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Denetim Aksiyon Şablonu");
        string[] headers = ["Bulgu Kodu*", "Aksiyon Açıklaması*", "Sorumlu", "Hedef Tarih (gg.AA.yyyy)", "Durum (Planlandı/Devam Ediyor/Tamamlandı/İptal)"];
        AddTemplateHeaders(ws, headers);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GetEthicsImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Etik Şablon");
        string[] headers = ["Konu*", "Açıklama*", "Kategori"];
        AddTemplateHeaders(ws, headers);
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AddTemplateHeaders(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }
        // Örnek satır
        var exampleCell = ws.Cell(2, 1);
        exampleCell.Value = "(örnek veri — bu satırı silin)";
        exampleCell.Style.Font.Italic = true;
        exampleCell.Style.Font.FontColor = XLColor.Gray;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    // Excel formula injection: '=' ile başlayan değerler formül olarak yorumlanır.
    // Kullanıcıdan gelen string'ler buradan geçirilmeli.
    // internal: RiskLibraryService kendi Excel export'unda da bu güvenli-hücre mantığını paylaşır.
    internal static string SafeCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        // XLSX değerleri XML olarak saklanır; XML 1.0 geçersiz kontrol karakterlerini (tab/LF/CR
        // dışındaki 0x00-0x1F) temizle — aksi halde dosya bozulur ya da Excel "onarım" uyarısı
        // verir. Word/PDF kopyala-yapıştır metni bu karakterleri sık içerir.
        if (value.Any(c => c < ' ' && c is not ('\t' or '\n' or '\r')))
            value = new string(value.Where(c => c >= ' ' || c is '\t' or '\n' or '\r').ToArray());
        if (value.Length == 0) return "";
        // Formül enjeksiyonu koruması (Excel/CSV): =,+,-,@ veya tab/CR ile başlayanı tırnakla.
        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    private static string StatusLabel(string status) => status switch
    {
        "proposed"           => "Önerildi",
        "under_review"       => "İncelemede",
        "awaiting_approval"  => "Onay Bekliyor",
        "approved"           => "Onaylandı",
        "strategy_set"       => "Strateji Belirlendi",
        "controlled"         => "Kontrol Altında",
        "residual_evaluated" => "Artık Risk Değerlendi",
        "action_planned"     => "Aksiyon Planlandı",
        "risk_accepted"      => "Risk Kabul Edildi",
        "rejected"           => "Reddedildi",
        _                    => status
    };

    private static string FindingStatusLabel(string status) => status switch
    {
        "open"               => "Açık",
        "closure_requested"  => "Kapanma Talep Edildi",
        "closed"             => "Kapalı",
        _                    => status
    };

    private static string ActionStatusLabel(string status) => status switch
    {
        "planned"     => "Planlandı",
        "in_progress" => "Devam Ediyor",
        "completed"   => "Tamamlandı",
        "cancelled"   => "İptal",
        _             => status
    };

    private static string NonconformityStatusLabel(string findingStatus, string? actionStatus)
    {
        var effective = actionStatus ?? findingStatus;
        return effective switch
        {
            "completed" or "closed"  => "Tamamlandı",
            "in_progress"            => "Devam Ediyor",
            "planned"                => "Açık",
            _                        => findingStatus is "closed" ? "Kapalı" : "Açık"
        };
    }

    private static string EthicsStatusLabel(string status) => status switch
    {
        "pending"                => "Bekliyor",
        "irrelevant"             => "İlgisiz",
        "ethics_board_notified"  => "Kurul Bildirimi",
        "disciplinary_referred"  => "Disiplin",
        "no_violation"           => "İhlal Yok",
        _                        => status
    };
}
