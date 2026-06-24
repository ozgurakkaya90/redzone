using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class RiskLibraryService(AppDbContext db)
{
    // ── Sorgulama ─────────────────────────────────────────────────────────────

    public List<RiskLibraryItem> Search(string? search, string? category, string? subCategory,
        string? sourceType, bool onlyActive = true)
    {
        var q = db.RiskLibraryItems.AsQueryable();
        if (onlyActive) q = q.Where(r => r.IsActive);
        if (!string.IsNullOrWhiteSpace(category))    q = q.Where(r => r.Category == category);
        if (!string.IsNullOrWhiteSpace(subCategory)) q = q.Where(r => r.SubCategory == subCategory);
        if (!string.IsNullOrWhiteSpace(sourceType))  q = q.Where(r => r.SourceType == sourceType);
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Kültür-bağımsız küçük harf: Türkçe kültürde ToLower() büyük 'I'yı noktasız 'ı'ya
            // çevirir ("Item" → "ıtem") ve MySQL'in noktalı LOWER()'ıyla eşleşmez → arama sessizce
            // boş döner. ToLowerInvariant istemci tarafında çalıştığından çeviri sorunu yaratmaz;
            // sütun tarafı r.Title.ToLower() ise SQL LOWER()'a çevrilir (MySQL'de zaten invariant).
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(r =>
                r.Title.ToLower().Contains(s) ||
                (r.Description != null && r.Description.ToLower().Contains(s)) ||
                (r.Tags != null && r.Tags.ToLower().Contains(s)) ||
                (r.SubCategory != null && r.SubCategory.ToLower().Contains(s)) ||
                (r.Hazard != null && r.Hazard.ToLower().Contains(s)));
        }
        return [.. q.OrderBy(r => r.Category).ThenBy(r => r.Title)];
    }

    public RiskLibraryItem? GetById(int id) => db.RiskLibraryItems.Find(id);

    public string[] GetCategories() =>
        [.. db.RiskLibraryItems.Where(r => r.IsActive && r.Category != null)
            .Select(r => r.Category!).Distinct().OrderBy(c => c)];

    public string[] GetSubCategories(string? category = null)
    {
        var q = db.RiskLibraryItems.Where(r => r.IsActive && r.SubCategory != null);
        if (!string.IsNullOrEmpty(category)) q = q.Where(r => r.Category == category);
        return [.. q.Select(r => r.SubCategory!).Distinct().OrderBy(c => c)];
    }

    public Dictionary<string, int> GetCategoryCounts() =>
        db.RiskLibraryItems
            .Where(r => r.IsActive && r.Category != null)
            .GroupBy(r => r.Category!)
            .ToDictionary(g => g.Key, g => g.Count());

    // ── Envanterе Önerme ──────────────────────────────────────────────────────

    public Risk ProposeFromLibrary(int libraryItemId, int proposedById)
    {
        var item = db.RiskLibraryItems.Find(libraryItemId)
            ?? throw new InvalidOperationException("Kütüphane kaydı bulunamadı.");

        var year = DateTime.UtcNow.Year;
        var risk = new Risk
        {
            Code                = $"R-{year}-{CounterHelper.GetNext(db, $"risk-{year}"):D3}",
            Title               = item.Title,
            Description         = item.Description,
            Category            = item.Category,
            SourceType          = item.SourceType,
            Hazard              = item.Hazard,
            PossibleImpact      = item.PossibleImpact,
            RiskStrategy        = item.RiskStrategy,
            Status              = "proposed",
            ProposedById        = proposedById,
            ProposedAt          = DateTime.UtcNow,
            SourceLibraryItemId = libraryItemId,
        };

        db.Risks.Add(risk);

        item.UsageCount++;
        db.SaveChanges();

        return risk;
    }

    public HashSet<int> GetProposedLibraryItemIds(int userId) =>
        [.. db.Risks
            .Where(r => r.ProposedById == userId && r.SourceLibraryItemId != null)
            .Select(r => r.SourceLibraryItemId!.Value)];

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public RiskLibraryItem Add(RiskLibraryItem item)
    {
        db.RiskLibraryItems.Add(item);
        db.SaveChanges();
        return item;
    }

    public void Update(RiskLibraryItem item)
    {
        db.RiskLibraryItems.Update(item);
        db.SaveChanges();
    }

    public void Delete(int id)
    {
        var item = db.RiskLibraryItems.Find(id);
        if (item is null) return;
        db.RiskLibraryItems.Remove(item);
        db.SaveChanges();
    }

    public void ToggleActive(int id)
    {
        var item = db.RiskLibraryItems.Find(id);
        if (item is null) return;
        item.IsActive = !item.IsActive;
        db.SaveChanges();
    }

    // ── İçe Aktarma (Excel) ──────────────────────────────────────────────────

    public ImportResult ImportFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var title = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(title)) { result.Skipped++; continue; }

                    var sourceRaw = row.Cell(5).GetString().Trim().ToLowerInvariant();
                    var sourceType = sourceRaw is "dış" or "dis" or "external" ? "external" : "internal";

                    var item = new RiskLibraryItem
                    {
                        Title           = title,
                        Description     = row.Cell(2).GetString().Trim().NullIfEmpty(),
                        Category        = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        SubCategory     = row.Cell(4).GetString().Trim().NullIfEmpty(),
                        SourceType      = sourceType,
                        Hazard          = row.Cell(6).GetString().Trim().NullIfEmpty(),
                        PossibleImpact  = row.Cell(7).GetString().Trim().NullIfEmpty(),
                        RiskStrategy    = row.Cell(8).GetString().Trim().NullIfEmpty(),
                        Tags            = row.Cell(9).GetString().Trim().NullIfEmpty(),
                        ReferenceStandard = row.Cell(10).GetString().Trim().NullIfEmpty(),
                        IsActive        = true,
                        CreatedAt       = DateTime.UtcNow,
                        CreatedById     = importedById,
                    };

                    db.RiskLibraryItems.Add(item);
                    db.SaveChanges();
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Seed ─────────────────────────────────────────────────────────────────

    public void SeedIfEmpty()
    {
        try
        {
            if (db.RiskLibraryItems.Any()) return;
            var items = RiskLibrarySeedData.GetItems();
            db.RiskLibraryItems.AddRange(items);
            db.SaveChanges();
        }
        catch
        {
            // Tablo henüz migrate edilmediyse sessizce geç
        }
    }

    // ── Dışa Aktarma şablonu ─────────────────────────────────────────────────

    public byte[] GetImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Risk Kütüphanesi Şablonu");
        string[] headers =
        [
            "Başlık*", "Açıklama", "Kategori", "Alt Kategori",
            "Kaynak Türü (İç/Dış)", "Tehlike/Kaynak", "Olası Etki",
            "Risk Stratejisi", "Etiketler", "Referans Standart"
        ];
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }
        var ex = ws.Cell(2, 1);
        ex.Value = "(örnek — bu satırı silin)";
        ex.Style.Font.Italic = true;
        ex.Style.Font.FontColor = XLColor.Gray;
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ExportToExcel(IEnumerable<RiskLibraryItem> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Risk Kütüphanesi");
        string[] headers =
        [
            "ID", "Başlık", "Açıklama", "Kategori", "Alt Kategori",
            "Kaynak Türü", "Tehlike", "Olası Etki", "Strateji", "Etiketler",
            "Referans", "Kullanım Sayısı", "Durum"
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
        foreach (var item in items)
        {
            ws.Cell(row, 1).Value  = item.Id;
            ws.Cell(row, 2).Value  = ExportService.SafeCell(item.Title);
            ws.Cell(row, 3).Value  = ExportService.SafeCell(item.Description);
            ws.Cell(row, 4).Value  = ExportService.SafeCell(item.Category);
            ws.Cell(row, 5).Value  = ExportService.SafeCell(item.SubCategory);
            ws.Cell(row, 6).Value  = item.SourceType == "internal" ? "İç" : "Dış";
            ws.Cell(row, 7).Value  = ExportService.SafeCell(item.Hazard);
            ws.Cell(row, 8).Value  = ExportService.SafeCell(item.PossibleImpact);
            ws.Cell(row, 9).Value  = ExportService.SafeCell(item.RiskStrategy);
            ws.Cell(row, 10).Value = ExportService.SafeCell(item.Tags);
            ws.Cell(row, 11).Value = ExportService.SafeCell(item.ReferenceStandard);
            ws.Cell(row, 12).Value = item.UsageCount;
            ws.Cell(row, 13).Value = item.IsActive ? "Aktif" : "Pasif";
            row++;
        }
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
