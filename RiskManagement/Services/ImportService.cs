using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class ImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool HasErrors => Errors.Count > 0;
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

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var title = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var sourceTypeRaw = row.Cell(4).GetString().Trim().ToLowerInvariant();
                    var sourceType = sourceTypeRaw is "dış" or "dis" or "external" ? "external" : "internal";

                    var risk = new Risk
                    {
                        Code         = $"R-{year}-{CounterHelper.GetNext(db, $"risk-{year}"):D3}",
                        Title        = title,
                        Description  = row.Cell(2).GetString().Trim().NullIfEmpty(),
                        Category     = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        SourceType   = sourceType,
                        RiskStrategy = row.Cell(5).GetString().Trim().NullIfEmpty(),
                        Hazard       = row.Cell(6).GetString().Trim().NullIfEmpty(),
                        PossibleImpact = row.Cell(7).GetString().Trim().NullIfEmpty(),
                        Status       = "proposed",
                        ProposedById = importedById,
                        ProposedAt   = DateTime.UtcNow,
                    };

                    db.Risks.Add(risk);
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

    // ── Kontrol Planı ─────────────────────────────────────────────────────────

    public ImportResult ImportControlsFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var riskCache = db.Risks.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
            var deptCache = db.Departments.ToDictionary(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var riskCode = row.Cell(1).GetString().Trim();
                    var description = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(riskCode) || string.IsNullOrWhiteSpace(description))
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (!riskCache.TryGetValue(riskCode, out var riskId))
                    {
                        result.Errors.Add($"Satır {rowNum}: '{riskCode}' kodlu risk bulunamadı.");
                        continue;
                    }

                    var deptName = row.Cell(6).GetString().Trim();
                    int? deptId = deptCache.TryGetValue(deptName, out var did) ? did : null;

                    var ctrl = new Control
                    {
                        RiskId      = riskId,
                        Description = description,
                        ControlType = NormalizeControlType(row.Cell(3).GetString().Trim()),
                        Effectiveness = row.Cell(4).GetString().Trim().NullIfEmpty(),
                        Frequency   = row.Cell(5).GetString().Trim().NullIfEmpty(),
                        OwnerDeptId = deptId,
                        EnteredById = importedById,
                        EnteredAt   = DateTime.UtcNow,
                    };

                    db.Controls.Add(ctrl);
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

    // ── Risk Aksiyon Planları ─────────────────────────────────────────────────

    public ImportResult ImportActionPlansFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var riskCache = db.Risks.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);
            var deptCache = db.Departments.ToDictionary(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var riskCode = row.Cell(1).GetString().Trim();
                    var desc     = row.Cell(2).GetString().Trim();
                    var resp     = row.Cell(3).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(riskCode) || string.IsNullOrWhiteSpace(desc) || string.IsNullOrWhiteSpace(resp))
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (!riskCache.TryGetValue(riskCode, out var riskId))
                    {
                        result.Errors.Add($"Satır {rowNum}: '{riskCode}' kodlu risk bulunamadı.");
                        continue;
                    }

                    var deptName = row.Cell(4).GetString().Trim();
                    int? deptId = deptCache.TryGetValue(deptName, out var did) ? did : null;

                    DateOnly? dueDate = null;
                    var dueDateStr = row.Cell(5).GetString().Trim();
                    if (!string.IsNullOrEmpty(dueDateStr) && DateOnly.TryParseExact(dueDateStr, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"], null, System.Globalization.DateTimeStyles.None, out var parsed))
                        dueDate = parsed;

                    var plan = new ActionPlan
                    {
                        RiskId      = riskId,
                        Description = desc,
                        Responsible = resp,
                        OwnerDeptId = deptId,
                        DueDate     = dueDate,
                        Status      = NormalizeActionStatus(row.Cell(6).GetString().Trim()),
                        CreatedById = importedById,
                        CreatedAt   = DateTime.UtcNow,
                    };

                    db.ActionPlans.Add(plan);
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

    // ── Denetim Bulguları ─────────────────────────────────────────────────────

    public ImportResult ImportFindingsFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var year = DateTime.UtcNow.Year;

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var title = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        result.Skipped++;
                        continue;
                    }

                    DateOnly? dueDate = null;
                    var dueDateStr = row.Cell(5).GetString().Trim();
                    if (!string.IsNullOrEmpty(dueDateStr) && DateOnly.TryParseExact(dueDateStr, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"], null, System.Globalization.DateTimeStyles.None, out var parsed))
                        dueDate = parsed;

                    var finding = new AuditFinding
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
                    };

                    db.AuditFindings.Add(finding);
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

    // ── Denetim Aksiyon Planları ──────────────────────────────────────────────

    public ImportResult ImportAuditActionsFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();

            var findingCache = db.AuditFindings.ToDictionary(f => f.Code, f => f.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var findingCode = row.Cell(1).GetString().Trim();
                    var desc        = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(findingCode) || string.IsNullOrWhiteSpace(desc))
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (!findingCache.TryGetValue(findingCode, out var findingId))
                    {
                        result.Errors.Add($"Satır {rowNum}: '{findingCode}' kodlu bulgu bulunamadı.");
                        continue;
                    }

                    DateOnly? dueDate = null;
                    var dueDateStr = row.Cell(4).GetString().Trim();
                    if (!string.IsNullOrEmpty(dueDateStr) && DateOnly.TryParseExact(dueDateStr, ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"], null, System.Globalization.DateTimeStyles.None, out var parsed))
                        dueDate = parsed;

                    var action = new AuditFindingAction
                    {
                        FindingId   = findingId,
                        Description = desc,
                        Responsible = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        DueDate     = dueDate,
                        Status      = NormalizeActionStatus(row.Cell(5).GetString().Trim()),
                        CreatedById = importedById,
                        CreatedAt   = DateTime.UtcNow,
                    };

                    db.AuditFindingActions.Add(action);
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

    // ── Etik Bildirimler ──────────────────────────────────────────────────────

    public ImportResult ImportEthicsFromExcel(Stream stream)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var year = DateTime.UtcNow.Year;

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var subject = row.Cell(1).GetString().Trim();
                    var desc    = row.Cell(2).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(desc))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var report = new EthicsReport
                    {
                        Code           = $"E-{year}-{CounterHelper.GetNext(db, $"ethics-{year}"):D3}",
                        Subject        = subject,
                        Description    = desc,
                        ReportCategory = row.Cell(3).GetString().Trim().NullIfEmpty(),
                        Status         = "pending",
                        SubmittedAt    = DateTime.UtcNow,
                    };

                    db.EthicsReports.Add(report);
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

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    private static string NormalizeControlType(string raw) => raw.ToLowerInvariant() switch
    {
        "tespit edici" or "tespit"  => "Tespit Edici",
        "düzeltici" or "duzeltici" or "corrective" => "Düzeltici",
        _ => "Önleyici"
    };

    private static string NormalizeActionStatus(string raw) => raw.ToLowerInvariant() switch
    {
        "devam ediyor" or "in_progress" or "devam"  => "in_progress",
        "tamamlandı" or "tamamlandi" or "completed" => "completed",
        "iptal" or "cancelled"                       => "cancelled",
        _                                             => "planned"
    };
}

internal static class StringExtensions
{
    internal static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
