using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class ImportResult
{
    public int Imported { get; set; }
    public int Updated  { get; set; }
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

    /// <summary>
    /// Risk envanterini Excel'den içe aktarır. Sütun düzeni dışa aktarma/şablon ile aynıdır:
    /// 1 Kod, 2 Başlık*, 3 Açıklama, 4 Kategori, 5 Kaynak Sınıflandırması, 6 Kaynak Türü,
    /// 7 Tehlike, 8 Olası Etki, 9 Faaliyet Alanı, 10 Etkilenecek Kişiler, 11 İlgili Mevzuat,
    /// 12 Risk Stratejisi, 13 Mevcut Durum, 14 Aktif/Pasif.
    /// Kod sütunu dolu ve mevcut bir riskle eşleşiyorsa o risk güncellenir; aksi halde yeni kayıt oluşturulur.
    /// </summary>
    public ImportResult ImportRisksFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.First();
            var year = DateTime.UtcNow.Year;

            var existingByCode = db.Risks.ToDictionary(r => r.Code, r => r, StringComparer.OrdinalIgnoreCase);
            var newCount = 0;
            var updCount = 0;

            foreach (var row in ws.RowsUsed().Skip(1))
            {
                var rowNum = row.RowNumber();
                try
                {
                    var title = row.Cell(2).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(title)) { result.Skipped++; continue; }

                    var code           = row.Cell(1).GetString().Trim();
                    var description    = row.Cell(3).GetString().Trim().NullIfEmpty();
                    var category       = row.Cell(4).GetString().Trim().NullIfEmpty();
                    var sourceType     = ParseSourceType(row.Cell(5).GetString());
                    var source         = row.Cell(6).GetString().Trim().NullIfEmpty();
                    var hazard         = row.Cell(7).GetString().Trim().NullIfEmpty();
                    var possibleImpact = row.Cell(8).GetString().Trim().NullIfEmpty();
                    var activityArea   = row.Cell(9).GetString().Trim().NullIfEmpty();
                    var affected       = ParsePersonList(row.Cell(10).GetString());
                    var legislation    = row.Cell(11).GetString().Trim().NullIfEmpty();
                    var strategy       = row.Cell(12).GetString().Trim().NullIfEmpty();
                    var currentStatus  = row.Cell(13).GetString().Trim().NullIfEmpty();
                    var isActive       = ParseActive(row.Cell(14).GetString());

                    if (!string.IsNullOrWhiteSpace(code) && existingByCode.TryGetValue(code, out var existing))
                    {
                        // Mevcut riski güncelle
                        existing.Title              = title;
                        existing.Description        = description;
                        existing.Category           = category;
                        existing.SourceType         = sourceType;
                        existing.Source             = source;
                        existing.Hazard             = hazard;
                        existing.PossibleImpact     = possibleImpact;
                        existing.ActivityArea       = activityArea;
                        existing.AffectedPersons    = Risk.SerializePersonsList(affected);
                        existing.RelevantLegislation = legislation;
                        existing.RiskStrategy       = strategy;
                        existing.CurrentStatus      = currentStatus;
                        existing.IsActive           = isActive;
                        updCount++;
                    }
                    else
                    {
                        var risk = new Risk
                        {
                            Code                = $"R-{year}-{CounterHelper.GetNext(db, $"risk-{year}"):D3}",
                            Title               = title,
                            Description         = description,
                            Category            = category,
                            SourceType          = sourceType,
                            Source              = source,
                            Hazard              = hazard,
                            PossibleImpact      = possibleImpact,
                            ActivityArea        = activityArea,
                            AffectedPersons     = Risk.SerializePersonsList(affected),
                            RelevantLegislation = legislation,
                            RiskStrategy        = strategy,
                            CurrentStatus       = currentStatus,
                            IsActive            = isActive,
                            Status              = "proposed",
                            ProposedById        = importedById,
                            ProposedAt          = DateTime.UtcNow,
                        };
                        db.Risks.Add(risk);
                        newCount++;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Satır {rowNum}: {ex.Message}");
                }
            }

            if (newCount > 0 || updCount > 0)
            {
                db.SaveChanges(); // tek transaction
                result.Imported = newCount;
                result.Updated  = updCount;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    private static string ParseSourceType(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        return v.Contains("dış") || v.Contains("dis") || v.Contains("external") ? "external" : "internal";
    }

    private static bool ParseActive(string raw)
    {
        var v = raw.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return true; // boş = aktif
        return !(v.Contains("pasif") || v.Contains("passive") || v is "hayır" or "hayir" or "false" or "0" or "kapalı" or "kapali");
    }

    private static List<string> ParsePersonList(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? []
        : raw.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

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

    // ── Dış Denetim Uygunsuzluk Aksiyon Planı ────────────────────────────────
    // Sütun düzeni (Resmi Denetim Uygunsuzluk Aksiyon Planı Excel formatı):
    //  1  Geçirilen Denetim Adı      → ExternalAudit.Subject
    //  2  Denetim Tarihi             → ExternalAudit.AuditDate
    //  3  Denetim Türü               → ExternalAudit.AuditType
    //  4  İlgili Mevzuat/Standart    → ExternalAudit.Standard (+ AuditingBody)
    //  5  İlgili Mevzuat Maddesi     → AuditFinding.StandardArticle
    //  6  İlgili Denetim Listesi Adı → ExternalAudit.ChecklistName
    //  7  Uygunsuzluk Tespit Edildi  → Evet/Hayır (Hayır ise sadece denetim kaydı)
    //  8  Uygunsuzluk Adedi          → AuditFinding.NonconformityCount ("3 adet" gibi)
    //  9  Uygunsuzluğa Konu Madde    → AuditFinding.StandardClause
    // 10  Uygunsuzluk Detay Açıklama → AuditFinding.Description + Title
    // 11  Majör/Minör                → AuditFinding.Severity
    // 12  Alınan Aksiyon             → AuditFindingAction.Description
    // 13  Sorumlu Departman          → AuditFindingAction.Responsible
    // 14  Termin                     → AuditFindingAction.DueDate
    // 15  Durum                      → AuditFinding.Status + AuditFindingAction.Status + ExternalAudit.Notes
    //
    // Çoklu sheet desteği: "Geçirilen Denetim" başlıklı tüm sheet'ler taranır.
    public ImportResult ImportExternalAuditNonconformitiesFromExcel(Stream stream, int importedById)
    {
        var result = new ImportResult();
        try
        {
            using var wb = new XLWorkbook(stream);
            var year = DateTime.UtcNow.Year;

            // Aynı import içinde oluşturulan/bulunan denetimleri önbelleğe al (tüm sheet'ler arası paylaşılır)
            var auditCache = new Dictionary<(string subject, DateOnly date), ExternalAudit>(
                EqualityComparer<(string, DateOnly)>.Default);
            foreach (var a in db.ExternalAudits.ToList())
                auditCache.TryAdd((a.Subject, a.AuditDate), a);

            // Uygunsuzluk formatındaki tüm sheet'leri işle
            foreach (var ws in wb.Worksheets)
            {
                // Header kontrolü: col 1 = "Geçirilen Denetim Adı" veya benzer
                var header1 = ws.Cell(1, 1).GetString().Trim();
                if (!header1.Contains("Denetim", StringComparison.OrdinalIgnoreCase)) continue;
                if (ws.LastColumnUsed()?.ColumnNumber() < 7) continue;

                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var rowNum = row.RowNumber();
                    var sheetCtx = $"[{ws.Name} Satır {rowNum}]";
                    try
                    {
                        var auditName = row.Cell(1).GetString().Trim();
                        if (string.IsNullOrWhiteSpace(auditName)) { result.Skipped++; continue; }

                        // ── Tarih ayrıştır ────────────────────────────────────────
                        DateOnly auditDate;
                        var dateCell = row.Cell(2);
                        if (dateCell.DataType == XLDataType.DateTime)
                            auditDate = DateOnly.FromDateTime(dateCell.GetDateTime());
                        else
                        {
                            var dateStr = dateCell.GetString().Trim();
                            if (!DateOnly.TryParseExact(dateStr,
                                    ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "d.M.yy"],
                                    null, System.Globalization.DateTimeStyles.None, out auditDate))
                            {
                                result.Errors.Add($"{sheetCtx}: Geçersiz denetim tarihi: '{dateStr}'");
                                continue;
                            }
                        }

                        var auditType       = row.Cell(3).GetString().Trim().NullIfEmpty();
                        var standard        = row.Cell(4).GetString().Trim().NullIfEmpty();
                        var standardArticle = row.Cell(5).GetString().Trim().NullIfEmpty();
                        var checklistName   = row.Cell(6).GetString().Trim().NullIfEmpty();
                        var detectedRaw     = row.Cell(7).GetString().Trim().ToLowerInvariant();
                        var countRaw        = row.Cell(8).GetString().Trim();
                        var standardClause  = row.Cell(9).GetString().Trim().NullIfEmpty();
                        var description     = row.Cell(10).GetString().Trim().NullIfEmpty();
                        var severityRaw     = row.Cell(11).GetString().Trim().NullIfEmpty();
                        var actionDesc      = row.Cell(12).GetString().Trim().NullIfEmpty();
                        var responsible     = row.Cell(13).GetString().Trim().NullIfEmpty();
                        var statusRaw       = row.Cell(15).GetString().Trim();
                        var actionStatus    = ParseNonconformityStatus(statusRaw);

                        // ── Termin tarihini ayrıştır ──────────────────────────────
                        DateOnly? termDate = null;
                        var termCell = row.Cell(14);
                        if (termCell.DataType == XLDataType.DateTime)
                            termDate = DateOnly.FromDateTime(termCell.GetDateTime());
                        else
                        {
                            var termStr = termCell.GetString().Trim();
                            if (!string.IsNullOrEmpty(termStr) &&
                                DateOnly.TryParseExact(termStr,
                                    ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "d.M.yy"],
                                    null, System.Globalization.DateTimeStyles.None, out var td))
                                termDate = td;
                        }

                        // ── ExternalAudit bul veya oluştur ────────────────────────
                        var auditKey = (auditName, auditDate);
                        if (!auditCache.TryGetValue(auditKey, out var audit))
                        {
                            var body      = standard ?? auditName;
                            var auditStat = ParseAuditStatus(statusRaw);
                            audit = new ExternalAudit
                            {
                                Code          = $"DD-{year}-{CounterHelper.GetNext(db, $"external-audit-{year}"):D3}",
                                Subject       = auditName,
                                AuditingBody  = body,
                                AuditDate     = auditDate,
                                AuditType     = auditType,
                                Standard      = standard,
                                ChecklistName = checklistName,
                                Status        = auditStat,
                                CreatedById   = importedById,
                                CreatedAt     = DateTime.UtcNow,
                            };
                            db.ExternalAudits.Add(audit);
                            db.SaveChanges();
                            auditCache[auditKey] = audit;
                        }
                        else
                        {
                            var changed = false;
                            if (audit.AuditType    is null && auditType    is not null) { audit.AuditType    = auditType;    changed = true; }
                            if (audit.ChecklistName is null && checklistName is not null) { audit.ChecklistName = checklistName; changed = true; }
                            if (changed) db.SaveChanges();
                        }

                        // ── Uygunsuzluk yoksa sadece denetim kaydını oluştur ──────
                        bool noFinding = detectedRaw.StartsWith("hay") || detectedRaw is "no" or "false";
                        if (noFinding)
                        {
                            result.Skipped++;
                            continue;
                        }

                        // Bulgu başlığı: standart maddesi → açıklama → denetim adı
                        var titleSrc = standardClause ?? description ?? auditName;
                        var title    = titleSrc.Length > 200 ? titleSrc[..197] + "…" : titleSrc;

                        // "3 adet" → 3 — sayıyı metinden çıkar
                        var countDigits = new string(countRaw.TakeWhile(char.IsDigit).ToArray());
                        int? count = int.TryParse(countDigits, out var n) && n > 0 ? n : null;

                        var findingStatus = actionStatus is "completed" ? "closed" : "open";

                        var finding = new AuditFinding
                        {
                            Code               = $"DB-{year}-{CounterHelper.GetNext(db, $"external-finding-{year}"):D3}",
                            Title              = title,
                            Description        = Trunc(description, 2000),
                            Severity           = ParseSeverity(severityRaw),
                            StandardArticle    = Trunc(standardArticle, 500),
                            StandardClause     = Trunc(standardClause, 1000),
                            NonconformityCount = count,
                            AuditSource        = "external",
                            ExternalAuditId    = audit.Id,
                            Status             = findingStatus,
                            DueDate            = termDate,
                            AuditorId          = importedById,
                            CreatedAt          = DateTime.UtcNow,
                        };
                        db.AuditFindings.Add(finding);
                        db.SaveChanges();

                        // Aksiyon: açıklama veya sorumlu veya tarih varsa oluştur
                        if (!string.IsNullOrWhiteSpace(actionDesc) || !string.IsNullOrWhiteSpace(responsible) || termDate.HasValue)
                        {
                            // Uzun serbest-metin durumu varsa aksiyon notuna ekle
                            var fullActionDesc = actionDesc
                                ?? (!string.IsNullOrWhiteSpace(statusRaw) ? $"Durum: {statusRaw}" : "(bakınız durum)");

                            db.AuditFindingActions.Add(new AuditFindingAction
                            {
                                FindingId   = finding.Id,
                                Description = Trunc(fullActionDesc, 1000),
                                Responsible = Trunc(responsible, 200),
                                DueDate     = termDate,
                                Status      = actionStatus,
                                // Uzun serbest-metin durum açıklamasını kapanış notuna yaz
                                ClosureNote = statusRaw.Length > 20 ? Trunc(statusRaw, 1000) : null,
                                CreatedById = importedById,
                                CreatedAt   = DateTime.UtcNow,
                            });
                            db.SaveChanges();
                        }

                        result.Imported++;
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"{sheetCtx}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okuma hatası: {ex.Message}");
        }
        return result;
    }

    private static string ParseSeverity(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var v = raw.Trim();
        return v.ToLowerInvariant() switch
        {
            "majör" or "major" or "kritik" or "critical" => "Majör",
            "minör" or "minor" or "düşük" or "low"       => "Minör",
            _                                             => v
        };
    }

    // "Tamamlandı.(Hekim adına...)" gibi uzun serbest-metin durumları işler
    private static string ParseNonconformityStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "planned";
        var v = raw.Trim().ToLowerInvariant();
        if (v.StartsWith("tamamland") || v.StartsWith("kapal"))     return "completed";
        if (v.Contains("devam") || v.Contains("süreç"))              return "in_progress";
        if (v.Contains("bekleni") || v.Contains("bilgi"))            return "in_progress";
        return "planned";
    }

    // ExternalAudit.Status için basit eşleştirme
    private static string ParseAuditStatus(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "completed";
        var v = raw.Trim().ToLowerInvariant();
        if (v.StartsWith("tamamland") || v.StartsWith("kapal"))      return "completed";
        if (v.Contains("devam") || v.Contains("süreç") || v.Contains("bekleni")) return "in_progress";
        return "completed";
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

    /// <summary>Alanın DB MaxLength sınırını aşmaması için kırpar; null güvenlidir.</summary>
    private static string? Trunc(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..(max - 1)] + "…";

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
