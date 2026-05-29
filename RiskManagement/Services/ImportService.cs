using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class ImportResult
{
    public int Imported { get; set; }
    public int Skipped  { get; set; }

    /// <summary>Satır bazlı hata açıklamaları (ör. "Satır 5: Başlık zorunlu").</summary>
    public List<string> Errors { get; set; } = [];

    public bool HasErrors => Errors.Count > 0;
    public bool IsSuccess => !HasErrors && Imported > 0;

    public void AddRowError(int rowNumber, string message) =>
        Errors.Add($"Satır {rowNumber}: {message}");
}

public class ImportService(AppDbContext db)
{
    // ── Risk Envanteri ────────────────────────────────────────────────────────

    public ImportResult ImportRisksFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var year = DateTime.UtcNow.Year;

            var toAdd = new List<Risk>();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var title = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(title)) { result.Skipped++; continue; }

                    var sourceTypeRaw = row.Cell(4).GetString().Trim().ToLowerInvariant();
                    var sourceType = sourceTypeRaw is "dış" or "dis" or "external" ? "external" : "internal";

                    var risk = new Risk
                    {
                        Code           = $"R-{year}-{CounterHelper.GetNext(db, $"risk-{year}"):D3}",
                        Title          = title,
                        Description    = row.Cell(2).GetString().Trim().NullIfEmpty(),
                        Category       = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        SourceType     = sourceType,
                        RiskStrategy   = row.Cell(5).GetString().Trim().NullIfEmpty(),
                        Hazard         = row.Cell(6).GetString().Trim().NullIfEmpty(),
                        PossibleImpact = row.Cell(7).GetString().Trim().NullIfEmpty(),
                        Status         = "proposed",
                        ProposedById   = importedById,
                        ProposedAt     = DateTime.UtcNow,
                    };

                    toAdd.Add(risk);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (toAdd.Count > 0)
            {
                db.Risks.AddRange(toAdd);
                db.SaveChanges(); // tek transaction — N ayrı round-trip yerine 1
                result.Imported = toAdd.Count;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Kontrol Planı ─────────────────────────────────────────────────────────

    public ImportResult ImportControlsFromExcel(Stream stream, int importedById, ISet<int>? allowedRiskIds = null)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var riskQuery = db.Risks.AsQueryable();
            if (allowedRiskIds is not null) riskQuery = riskQuery.Where(r => allowedRiskIds.Contains(r.Id));
            var riskCache = riskQuery.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
            var deptCache = db.Departments.ToDictionary(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<Control>();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var riskCode = row.Cell(1).GetString().Trim();
                    var description = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(riskCode) || string.IsNullOrWhiteSpace(description))
                    { result.Skipped++; continue; }

                    if (!riskCache.TryGetValue(riskCode, out var riskId))
                    { result.Errors.Add($"Satır {rowNum}: '{riskCode}' kodlu risk bulunamadı."); continue; }

                    var deptName = row.Cell(6).GetString().Trim();
                    int? deptId = deptCache.TryGetValue(deptName, out var did) ? did : null;

                    toAdd.Add(new Control
                    {
                        RiskId        = riskId,
                        Description   = description,
                        ControlType   = NormalizeControlType(row.Cell(3).GetString().Trim()),
                        Effectiveness = row.Cell(4).GetString().Trim().NullIfEmpty(),
                        Frequency     = row.Cell(5).GetString().Trim().NullIfEmpty(),
                        OwnerDeptId   = deptId,
                        EnteredById   = importedById,
                        EnteredAt     = DateTime.UtcNow,
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (toAdd.Count > 0)
            {
                db.Controls.AddRange(toAdd);
                db.SaveChanges();
                result.Imported = toAdd.Count;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Risk Aksiyon Planları ─────────────────────────────────────────────────

    public ImportResult ImportActionPlansFromExcel(Stream stream, int importedById, ISet<int>? allowedRiskIds = null)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var riskQuery = db.Risks.AsQueryable();
            if (allowedRiskIds is not null) riskQuery = riskQuery.Where(r => allowedRiskIds.Contains(r.Id));
            var riskCache = riskQuery.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
            var deptCache = db.Departments.ToDictionary(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<ActionPlan>();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var riskCode = row.Cell(1).GetString().Trim();
                    var desc     = row.Cell(2).GetString().Trim();
                    var resp     = row.Cell(3).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(riskCode) || string.IsNullOrWhiteSpace(desc) || string.IsNullOrWhiteSpace(resp))
                    { result.Skipped++; continue; }

                    if (!riskCache.TryGetValue(riskCode, out var riskId))
                    { result.Errors.Add($"Satır {rowNum}: '{riskCode}' kodlu risk bulunamadı."); continue; }

                    var deptName = row.Cell(4).GetString().Trim();
                    int? deptId = deptCache.TryGetValue(deptName, out var did) ? did : null;

                    DateOnly? dueDate = null;
                    var dueDateStr = row.Cell(5).GetString().Trim();
                    if (!string.IsNullOrEmpty(dueDateStr) &&
                        DateOnly.TryParseExact(dueDateStr, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"],
                            null, System.Globalization.DateTimeStyles.None, out var parsed))
                        dueDate = parsed;

                    toAdd.Add(new ActionPlan
                    {
                        RiskId      = riskId,
                        Description = desc,
                        Responsible = resp,
                        OwnerDeptId = deptId,
                        DueDate     = dueDate,
                        Status      = NormalizeActionStatus(row.Cell(6).GetString().Trim()),
                        CreatedById = importedById,
                        CreatedAt   = DateTime.UtcNow,
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (toAdd.Count > 0)
            {
                db.ActionPlans.AddRange(toAdd);
                db.SaveChanges();
                result.Imported = toAdd.Count;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Denetim Bulguları ─────────────────────────────────────────────────────

    public ImportResult ImportFindingsFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var year = DateTime.UtcNow.Year;

            var toAdd = new List<AuditFinding>();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var title = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(title)) { result.Skipped++; continue; }

                    DateOnly? dueDate = null;
                    var dueDateStr = row.Cell(5).GetString().Trim();
                    if (!string.IsNullOrEmpty(dueDateStr) &&
                        DateOnly.TryParseExact(dueDateStr, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"],
                            null, System.Globalization.DateTimeStyles.None, out var parsed))
                        dueDate = parsed;

                    toAdd.Add(new AuditFinding
                    {
                        Code        = $"B-{year}-{CounterHelper.GetNext(db, $"finding-{year}"):D3}",
                        Title       = title,
                        Description = row.Cell(2).GetString().Trim().NullIfEmpty(),
                        Category    = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        Severity    = row.Cell(4).GetString().Trim().NullIfEmpty(),
                        DueDate     = dueDate,
                        Status      = "open",
                        AuditorId   = importedById,
                        CreatedAt   = DateTime.UtcNow,
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (toAdd.Count > 0)
            {
                db.AuditFindings.AddRange(toAdd);
                db.SaveChanges();
                result.Imported = toAdd.Count;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Denetim Aksiyon Planları ──────────────────────────────────────────────

    public ImportResult ImportAuditActionsFromExcel(Stream stream, int importedById, ISet<int>? allowedFindingIds = null)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var findingQuery = db.AuditFindings.AsQueryable();
            if (allowedFindingIds is not null) findingQuery = findingQuery.Where(f => allowedFindingIds.Contains(f.Id));
            var findingCache = findingQuery.ToDictionary(f => f.Code, f => f.Id, StringComparer.OrdinalIgnoreCase);

            var toAdd = new List<AuditFindingAction>();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var findingCode = row.Cell(1).GetString().Trim();
                    var desc        = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(findingCode) || string.IsNullOrWhiteSpace(desc))
                    { result.Skipped++; continue; }

                    if (!findingCache.TryGetValue(findingCode, out var findingId))
                    { result.Errors.Add($"Satır {rowNum}: '{findingCode}' kodlu bulgu bulunamadı."); continue; }

                    DateOnly? dueDate = null;
                    var dueDateStr = row.Cell(4).GetString().Trim();
                    if (!string.IsNullOrEmpty(dueDateStr) &&
                        DateOnly.TryParseExact(dueDateStr, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"],
                            null, System.Globalization.DateTimeStyles.None, out var parsed))
                        dueDate = parsed;

                    toAdd.Add(new AuditFindingAction
                    {
                        FindingId   = findingId,
                        Description = desc,
                        Responsible = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        DueDate     = dueDate,
                        Status      = NormalizeActionStatus(row.Cell(5).GetString().Trim()),
                        CreatedById = importedById,
                        CreatedAt   = DateTime.UtcNow,
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (toAdd.Count > 0)
            {
                db.AuditFindingActions.AddRange(toAdd);
                db.SaveChanges();
                result.Imported = toAdd.Count;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Etik Bildirimler ──────────────────────────────────────────────────────

    public ImportResult ImportEthicsFromExcel(Stream stream)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var year = DateTime.UtcNow.Year;

            var toAdd = new List<EthicsReport>();
            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var subject = row.Cell(1).GetString().Trim();
                    var desc    = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(desc))
                    { result.Skipped++; continue; }

                    toAdd.Add(new EthicsReport
                    {
                        Code           = $"EB-{year}-{CounterHelper.GetNext(db, $"ethics-{year}"):D3}",
                        Subject        = subject,
                        Description    = desc,
                        ReportCategory = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        Status         = "pending",
                        SubmittedAt    = DateTime.UtcNow,
                    });
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (toAdd.Count > 0)
            {
                db.EthicsReports.AddRange(toAdd);
                db.SaveChanges();
                result.Imported = toAdd.Count;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static string NormalizeControlType(string raw) => raw.ToLowerInvariant() switch
    {
        "tespit edici" or "tespit"                       => "Tespit Edici",
        "düzeltici" or "duzeltici" or "corrective"       => "Düzeltici",
        _                                                 => "Önleyici"
    };

    private static string NormalizeActionStatus(string raw) => raw.ToLowerInvariant() switch
    {
        "devam ediyor" or "in_progress" or "devam"       => "in_progress",
        "tamamlandı" or "tamamlandi" or "completed"      => "completed",
        "iptal" or "cancelled"                           => "cancelled",
        _                                                 => "planned"
    };
}

internal static class StringExtensions
{
    internal static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
