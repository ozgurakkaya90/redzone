using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class ExportService
{
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
        QuestPDF.Settings.License = LicenseType.Community;

        var riskList = risks.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("RİSK KAYDI RAPORU").Bold().FontSize(14).FontColor("#1E3A5F");
                        });
                        row.ConstantItem(150).AlignRight().Text(t =>
                        {
                            if (!string.IsNullOrEmpty(companyName))
                                t.Line(companyName).FontSize(9).FontColor("#64748B");
                            t.Line(DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR")))
                             .FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1E3A5F");
                    col.Item().PaddingBottom(4).Text($"Toplam {riskList.Count} risk kaydı").FontSize(8).FontColor("#64748B");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);  // Kod
                        cols.RelativeColumn(3);   // Başlık
                        cols.RelativeColumn(1.5f);// Kategori
                        cols.RelativeColumn(1.5f);// Durum
                        cols.ConstantColumn(45);  // Skor
                        cols.RelativeColumn(1.5f);// Seviye
                        cols.RelativeColumn(2);   // Sorumlu
                        cols.RelativeColumn(1.5f);// Departman
                    });

                    // Başlık satırı
                    static IContainer HeaderCell(IContainer c) => c
                        .Background("#1E3A5F").Padding(5);

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Kod", "Başlık", "Kategori", "Durum", "Skor", "Seviye", "Sorumlu", "Departman" })
                            h.Cell().Element(HeaderCell).Text(label).Bold().FontColor(Colors.White).FontSize(8);
                    });

                    // Veri satırları
                    bool alt = false;
                    foreach (var r in riskList)
                    {
                        var initial = r.Evaluations.FirstOrDefault(e => e.EvalType == "initial");
                        var bg = alt ? "#F8FAFC" : "#FFFFFF";
                        alt = !alt;

                        IContainer DataCell(IContainer c) => c.Background(bg).BorderBottom(1).BorderColor("#E2E8F0").Padding(4);

                        table.Cell().Element(DataCell).Text(r.Code).FontSize(8);
                        table.Cell().Element(DataCell).Text(r.Title).FontSize(8);
                        table.Cell().Element(DataCell).Text(r.Category ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(StatusLabel(r.Status)).FontSize(8);
                        table.Cell().Element(DataCell).Text(initial?.Score.ToString("F1") ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(initial?.RiskLevel ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(r.Owner?.FullName ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(r.Department?.Name ?? "—").FontSize(8);
                    }
                });

                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(8).FontColor("#94A3B8");
                        t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                        t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                        t.TotalPages().FontSize(8).FontColor("#94A3B8");
                    });
            });
        }).GeneratePdf();
    }

    public byte[] ExportFindingsToPdf(IEnumerable<AuditFinding> findings, string companyName = "")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var list = findings.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("İÇ DENETİM BULGULARI RAPORU").Bold().FontSize(14).FontColor("#1E3A5F");
                        });
                        row.ConstantItem(150).AlignRight().Text(t =>
                        {
                            if (!string.IsNullOrEmpty(companyName))
                                t.Line(companyName).FontSize(9).FontColor("#64748B");
                            t.Line(DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR")))
                             .FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1E3A5F");
                    col.Item().PaddingBottom(4).Text($"Toplam {list.Count} bulgu").FontSize(8).FontColor("#64748B");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);  // Kod
                        cols.RelativeColumn(3);   // Başlık
                        cols.RelativeColumn(1.5f);// Ciddiyet
                        cols.RelativeColumn(1.5f);// Durum
                        cols.RelativeColumn(2);   // Denetçi
                        cols.RelativeColumn(2);   // Bulgu Sahibi
                        cols.RelativeColumn(1.5f);// Departman
                        cols.ConstantColumn(60);  // Bitiş Tarihi
                    });

                    static IContainer HeaderCell(IContainer c) => c
                        .Background("#1E3A5F").Padding(5);

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Kod", "Başlık", "Ciddiyet", "Durum", "Denetçi", "Bulgu Sahibi", "Departman", "Bitiş Tarihi" })
                            h.Cell().Element(HeaderCell).Text(label).Bold().FontColor(Colors.White).FontSize(8);
                    });

                    bool alt = false;
                    foreach (var f in list)
                    {
                        var bg = alt ? "#F8FAFC" : "#FFFFFF";
                        alt = !alt;

                        IContainer DataCell(IContainer c) => c.Background(bg).BorderBottom(1).BorderColor("#E2E8F0").Padding(4);

                        table.Cell().Element(DataCell).Text(f.Code).FontSize(8);
                        table.Cell().Element(DataCell).Text(f.Title).FontSize(8);
                        table.Cell().Element(DataCell).Text(f.Severity ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(FindingStatusLabel(f.Status)).FontSize(8);
                        table.Cell().Element(DataCell).Text(f.Auditor?.FullName ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(f.Owner?.FullName ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(f.Department?.Name ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(f.DueDate?.ToString("dd.MM.yyyy") ?? "—").FontSize(8);
                    }
                });

                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(8).FontColor("#94A3B8");
                        t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                        t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                        t.TotalPages().FontSize(8).FontColor("#94A3B8");
                    });
            });
        }).GeneratePdf();
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
        QuestPDF.Settings.License = LicenseType.Community;
        var list = controls.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t => t.Span("KONTROL PLANI RAPORU").Bold().FontSize(14).FontColor("#1E3A5F"));
                        row.ConstantItem(150).AlignRight().Text(t =>
                        {
                            if (!string.IsNullOrEmpty(companyName)) t.Line(companyName).FontSize(9).FontColor("#64748B");
                            t.Line(DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))).FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1E3A5F");
                    col.Item().PaddingBottom(4).Text($"Toplam {list.Count} kontrol kaydı").FontSize(8).FontColor("#64748B");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(2);
                    });

                    static IContainer HeaderCell(IContainer c) => c.Background("#1E3A5F").Padding(5);

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Risk Kodu", "Kontrol Açıklaması", "Tür", "Etkinlik", "Sıklık", "Sahibi" })
                            h.Cell().Element(HeaderCell).Text(label).Bold().FontColor(Colors.White).FontSize(8);
                    });

                    bool alt = false;
                    foreach (var c in list)
                    {
                        var bg = alt ? "#F8FAFC" : "#FFFFFF";
                        alt = !alt;
                        IContainer DataCell(IContainer x) => x.Background(bg).BorderBottom(1).BorderColor("#E2E8F0").Padding(4);
                        table.Cell().Element(DataCell).Text(c.Risk?.Code ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(c.Description).FontSize(8);
                        table.Cell().Element(DataCell).Text(c.ControlType).FontSize(8);
                        table.Cell().Element(DataCell).Text(c.Effectiveness ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(c.Frequency ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(c.OwnerDept?.Name ?? "—").FontSize(8);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Sayfa ").FontSize(8).FontColor("#94A3B8");
                    t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                    t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                    t.TotalPages().FontSize(8).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf();
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
        QuestPDF.Settings.License = LicenseType.Community;
        var list = plans.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t => t.Span("RİSK AKSİYON PLANLARI RAPORU").Bold().FontSize(14).FontColor("#1E3A5F"));
                        row.ConstantItem(150).AlignRight().Text(t =>
                        {
                            if (!string.IsNullOrEmpty(companyName)) t.Line(companyName).FontSize(9).FontColor("#64748B");
                            t.Line(DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))).FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1E3A5F");
                    col.Item().PaddingBottom(4).Text($"Toplam {list.Count} aksiyon kaydı").FontSize(8).FontColor("#64748B");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.RelativeColumn(1.5f);
                        cols.ConstantColumn(70);
                        cols.RelativeColumn(1.5f);
                    });

                    static IContainer HeaderCell(IContainer c) => c.Background("#1E3A5F").Padding(5);

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Risk Kodu", "Aksiyon", "Sorumlu", "Departman", "Hedef Tarih", "Durum" })
                            h.Cell().Element(HeaderCell).Text(label).Bold().FontColor(Colors.White).FontSize(8);
                    });

                    bool alt = false;
                    foreach (var a in list)
                    {
                        var bg = alt ? "#F8FAFC" : "#FFFFFF";
                        alt = !alt;
                        IContainer DataCell(IContainer x) => x.Background(bg).BorderBottom(1).BorderColor("#E2E8F0").Padding(4);
                        table.Cell().Element(DataCell).Text(a.Risk?.Code ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(a.Description).FontSize(8);
                        table.Cell().Element(DataCell).Text(a.Responsible).FontSize(8);
                        table.Cell().Element(DataCell).Text(a.OwnerDept?.Name ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(a.DueDate?.ToString("dd.MM.yyyy") ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(ActionStatusLabel(a.Status)).FontSize(8);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Sayfa ").FontSize(8).FontColor("#94A3B8");
                    t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                    t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                    t.TotalPages().FontSize(8).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf();
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
        QuestPDF.Settings.License = LicenseType.Community;
        var list = actions.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t => t.Span("DENETİM AKSİYON PLANLARI RAPORU").Bold().FontSize(14).FontColor("#1E3A5F"));
                        row.ConstantItem(150).AlignRight().Text(t =>
                        {
                            if (!string.IsNullOrEmpty(companyName)) t.Line(companyName).FontSize(9).FontColor("#64748B");
                            t.Line(DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))).FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1E3A5F");
                    col.Item().PaddingBottom(4).Text($"Toplam {list.Count} aksiyon kaydı").FontSize(8).FontColor("#64748B");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(2);
                        cols.ConstantColumn(70);
                        cols.RelativeColumn(1.5f);
                    });

                    static IContainer HeaderCell(IContainer c) => c.Background("#1E3A5F").Padding(5);

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Bulgu Kodu", "Aksiyon", "Sorumlu", "Hedef Tarih", "Durum" })
                            h.Cell().Element(HeaderCell).Text(label).Bold().FontColor(Colors.White).FontSize(8);
                    });

                    bool alt = false;
                    foreach (var a in list)
                    {
                        var bg = alt ? "#F8FAFC" : "#FFFFFF";
                        alt = !alt;
                        IContainer DataCell(IContainer x) => x.Background(bg).BorderBottom(1).BorderColor("#E2E8F0").Padding(4);
                        table.Cell().Element(DataCell).Text(a.Finding?.Code ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(a.Description).FontSize(8);
                        table.Cell().Element(DataCell).Text(a.Responsible ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(a.DueDate?.ToString("dd.MM.yyyy") ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(ActionStatusLabel(a.Status)).FontSize(8);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Sayfa ").FontSize(8).FontColor("#94A3B8");
                    t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                    t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                    t.TotalPages().FontSize(8).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf();
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
        QuestPDF.Settings.License = LicenseType.Community;
        var list = reports.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(t => t.Span("ETİK BİLDİRİMLER RAPORU").Bold().FontSize(14).FontColor("#1E3A5F"));
                        row.ConstantItem(150).AlignRight().Text(t =>
                        {
                            if (!string.IsNullOrEmpty(companyName)) t.Line(companyName).FontSize(9).FontColor("#64748B");
                            t.Line(DateTime.Now.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("tr-TR"))).FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1E3A5F");
                    col.Item().PaddingBottom(4).Text($"Toplam {list.Count} etik bildirim").FontSize(8).FontColor("#64748B");
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn(3);
                        cols.RelativeColumn(1.5f);
                        cols.RelativeColumn(1.5f);
                        cols.ConstantColumn(70);
                    });

                    static IContainer HeaderCell(IContainer c) => c.Background("#1E3A5F").Padding(5);

                    table.Header(h =>
                    {
                        foreach (var label in new[] { "Kod", "Konu", "Kategori", "Durum", "Tarih" })
                            h.Cell().Element(HeaderCell).Text(label).Bold().FontColor(Colors.White).FontSize(8);
                    });

                    bool alt = false;
                    foreach (var r in list)
                    {
                        var bg = alt ? "#F8FAFC" : "#FFFFFF";
                        alt = !alt;
                        IContainer DataCell(IContainer x) => x.Background(bg).BorderBottom(1).BorderColor("#E2E8F0").Padding(4);
                        table.Cell().Element(DataCell).Text(r.Code).FontSize(8);
                        table.Cell().Element(DataCell).Text(r.Subject).FontSize(8);
                        table.Cell().Element(DataCell).Text(r.ReportCategory ?? "—").FontSize(8);
                        table.Cell().Element(DataCell).Text(EthicsStatusLabel(r.Status)).FontSize(8);
                        table.Cell().Element(DataCell).Text(r.SubmittedAt.ToString("dd.MM.yyyy")).FontSize(8);
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Sayfa ").FontSize(8).FontColor("#94A3B8");
                    t.CurrentPageNumber().FontSize(8).FontColor("#94A3B8");
                    t.Span(" / ").FontSize(8).FontColor("#94A3B8");
                    t.TotalPages().FontSize(8).FontColor("#94A3B8");
                });
            });
        }).GeneratePdf();
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
    private static string SafeCell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
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
