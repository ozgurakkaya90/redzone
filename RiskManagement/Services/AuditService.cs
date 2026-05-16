using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuditService(AppDbContext db)
{
    // ─── Audit Plan ───────────────────────────────────────────────────────────

    public AuditPlan? GetPlan(int year) =>
        db.AuditPlans
            .Include(p => p.CreatedBy)
            .Include(p => p.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.PlannedStartDate))
                .ThenInclude(i => i.Responsible)
            .Include(p => p.Items)
                .ThenInclude(i => i.Department)
                    .ThenInclude(d => d!.Organization)
                        .ThenInclude(o => o!.Company)
            .FirstOrDefault(p => p.Year == year);

    public List<int> GetPlanYears() =>
        [.. db.AuditPlans.Select(p => p.Year).Distinct().OrderByDescending(y => y)];

    public AuditPlan EnsurePlan(int year, string title, int userId)
    {
        var plan = db.AuditPlans.FirstOrDefault(p => p.Year == year);
        if (plan != null) return plan;
        plan = new AuditPlan { Year = year, Title = title, CreatedById = userId };
        db.AuditPlans.Add(plan);
        db.SaveChanges();
        return plan;
    }

    public void UpdatePlan(AuditPlan plan)
    {
        db.AuditPlans.Update(plan);
        db.SaveChanges();
    }

    public AuditPlanItem AddPlanItem(AuditPlanItem item)
    {
        var maxOrder = db.AuditPlanItems
            .Where(i => i.AuditPlanId == item.AuditPlanId)
            .Select(i => (int?)i.SortOrder).Max() ?? 0;
        item.SortOrder = maxOrder + 1;
        db.AuditPlanItems.Add(item);
        db.SaveChanges();
        return item;
    }

    public void UpdatePlanItem(AuditPlanItem item)
    {
        db.AuditPlanItems.Update(item);
        db.SaveChanges();
    }

    public void DeletePlanItem(int itemId)
    {
        var item = db.AuditPlanItems.Find(itemId);
        if (item is null) return;
        db.AuditPlanItems.Remove(item);
        db.SaveChanges();
    }

    public static string GetItemStatus(AuditPlanItem item)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (item.ActualEndDate.HasValue)
            return item.ActualEndDate.Value <= item.PlannedEndDate ? "completed" : "completed_late";
        if (item.ActualStartDate.HasValue) return "in_progress";
        if (today > item.PlannedEndDate) return "late";
        return "planned";
    }

    // ─── Internal Audits ──────────────────────────────────────────────────────

    // ─── Dosya yükleme sabitleri (tek kaynak) ────────────────────────────────
    public static readonly HashSet<string> AllowedAttachmentExtensions =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".zip"];
    public const long MaxAttachmentSize = 10L * 1024 * 1024; // 10 MB

    public string GenerateAuditCode()
    {
        var year = DateTime.UtcNow.Year;
        return $"ID-{year}-{CounterHelper.GetNext(db, $"audit-{year}"):D3}";
    }

    // Temel sorgu — sadece her zaman var olan kolonları içerir
    public IQueryable<InternalAudit> AuditQuery() => db.InternalAudits
        .Include(a => a.LeadAuditor)
        .Include(a => a.Findings);

    // Yeni kolonları içeren genişletilmiş sorgu — sadece migration uygulandıktan sonra kullanılır
    public IQueryable<InternalAudit> AuditQueryFull() => db.InternalAudits
        .Include(a => a.LeadAuditor)
        .Include(a => a.Department).ThenInclude(d => d!.Organization).ThenInclude(o => o!.Company)
        .Include(a => a.AuditPlanItem).ThenInclude(i => i!.Plan)
        .Include(a => a.Findings);

    public List<InternalAudit> GetAudits(string? statusFilter = null)
    {
        var q = AuditQuery();
        if (!string.IsNullOrEmpty(statusFilter)) q = q.Where(a => a.Status == statusFilter);
        return [.. q.OrderByDescending(a => a.CreatedAt)];
    }

    public List<InternalAudit> GetAuditsForUser(int userId, string role, string? statusFilter = null)
    {
        var q = AuditQuery();
        if (!string.IsNullOrEmpty(statusFilter)) q = q.Where(a => a.Status == statusFilter);
        q = ScopeAudits(q, userId, role);
        return [.. q.OrderByDescending(a => a.CreatedAt)];
    }

    public InternalAudit? GetAudit(int id)
    {
        try   { return AuditQueryFull().FirstOrDefault(a => a.Id == id); }
        catch { return AuditQuery().FirstOrDefault(a => a.Id == id); }
    }

    public InternalAudit? GetAuditForUser(int id, int userId, string role)
    {
        var audit = GetAudit(id);
        return audit != null && CanAccessAudit(audit, userId, role) ? audit : null;
    }

    private IQueryable<InternalAudit> ScopeAudits(IQueryable<InternalAudit> q, int userId, string role)
    {
        if (role is "admin" or "audit_manager") return q;
        if (role == "auditor")
            return q.Where(a => a.LeadAuditorId == userId || a.Findings.Any(f => f.AuditorId == userId));
        return q.Where(_ => false);
    }

    public bool CanAccessAudit(InternalAudit audit, int userId, string role)
    {
        if (role is "admin" or "audit_manager") return true;
        return role == "auditor" &&
            (audit.LeadAuditorId == userId || audit.Findings.Any(f => f.AuditorId == userId));
    }

    public InternalAudit CreateAudit(string title, string? auditType, string? auditedUnit,
        string? scope, string period, DateOnly? startDate, DateOnly? endDate, int leadAuditorId,
        int? departmentId = null, int? auditPlanItemId = null)
    {
        var audit = new InternalAudit
        {
            Code           = GenerateAuditCode(),
            Title          = title,
            AuditType      = auditType,
            AuditedUnit    = auditedUnit,
            Scope          = scope,
            Period         = period,
            StartDate      = startDate,
            EndDate        = endDate,
            LeadAuditorId  = leadAuditorId,
            DepartmentId   = departmentId,
            AuditPlanItemId= auditPlanItemId,
        };
        db.InternalAudits.Add(audit);
        db.SaveChanges();
        return audit;
    }

    /// <summary>
    /// Denetim planındaki bir maddeden otomatik iç denetim oluşturur.
    /// Madde zaten bir denetime bağlıysa hata fırlatır.
    /// </summary>
    public InternalAudit CreateAuditFromPlanItem(int planItemId, int leadAuditorId)
    {
        var item = db.AuditPlanItems
            .Include(i => i.Department)
            .Include(i => i.Plan)
            .FirstOrDefault(i => i.Id == planItemId)
            ?? throw new InvalidOperationException("Plan maddesi bulunamadı.");

        if (db.InternalAudits.Any(a => a.AuditPlanItemId == planItemId))
            throw new InvalidOperationException("Bu plan maddesine zaten bir iç denetim bağlı.");

        var unitName = item.Department?.Name ?? item.AuditedUnit;
        var period   = $"{item.Plan.Year}";

        var audit = CreateAudit(
            title          : item.Title,
            auditType      : item.AuditType,
            auditedUnit    : unitName,
            scope          : item.Description,
            period         : period,
            startDate      : item.PlannedStartDate,
            endDate        : item.PlannedEndDate,
            leadAuditorId  : leadAuditorId,
            departmentId   : item.DepartmentId,
            auditPlanItemId: planItemId);

        return audit;
    }

    public bool UpdateAudit(int id, string title, string? auditType, string? auditedUnit,
        string? scope, string period, DateOnly? startDate, DateOnly? endDate, string status,
        int? departmentId = null)
    {
        var audit = db.InternalAudits.Find(id);
        if (audit == null) return false;
        audit.Title        = title;
        audit.AuditType    = auditType;
        audit.AuditedUnit  = auditedUnit;
        audit.Scope        = scope;
        audit.Period       = period;
        audit.StartDate    = startDate;
        audit.EndDate      = endDate;
        audit.Status       = status;
        audit.DepartmentId = departmentId ?? audit.DepartmentId;
        db.SaveChanges();

        // Plan maddesinin actual tarihlerini güncelle
        SyncPlanItemDates(audit);

        return true;
    }

    /// <summary>
    /// İç denetimin durumuna göre bağlı plan maddesinin gerçekleşen tarihlerini günceller.
    /// </summary>
    private void SyncPlanItemDates(InternalAudit audit)
    {
        if (audit.AuditPlanItemId == null) return;
        var item = db.AuditPlanItems.Find(audit.AuditPlanItemId);
        if (item == null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        if (audit.Status == "in_progress" && item.ActualStartDate == null)
            item.ActualStartDate = audit.StartDate ?? today;

        if (audit.Status == "completed" && item.ActualEndDate == null)
        {
            if (item.ActualStartDate == null)
                item.ActualStartDate = audit.StartDate ?? today;
            item.ActualEndDate = audit.EndDate ?? today;
        }

        db.SaveChanges();
    }

    /// <summary>Plan maddesine bağlı InternalAudit'i döner (yoksa null).</summary>
    public InternalAudit? GetAuditByPlanItem(int planItemId)
    {
        try   { return AuditQuery().FirstOrDefault(a => a.AuditPlanItemId == planItemId); }
        catch { return null; }
    }

    // ─── Findings ─────────────────────────────────────────────────────────────

    public string GenerateFindingCode()
    {
        var year = DateTime.UtcNow.Year;
        return $"B-{year}-{CounterHelper.GetNext(db, $"finding-{year}"):D3}";
    }

    public IQueryable<AuditFinding> FindingQuery() => db.AuditFindings
        .Include(f => f.Auditor)
        .Include(f => f.Owner)
        .Include(f => f.Department)
        .Include(f => f.InternalAudit)
        .Include(f => f.ClosureRequests).ThenInclude(c => c.RequestedBy)
        .Include(f => f.ClosureRequests).ThenInclude(c => c.ReviewedBy)
        .Include(f => f.Actions).ThenInclude(a => a.CreatedBy)
        .Include(f => f.Attachments).ThenInclude(a => a.UploadedBy);

    public List<AuditFinding> GetFindings(string? category = null, string? severity = null,
        string? status = null, int? auditId = null)
    {
        var q = FindingQuery();
        if (!string.IsNullOrEmpty(category)) q = q.Where(f => f.Category == category);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(f => f.Severity == severity);
        if (!string.IsNullOrEmpty(status))   q = q.Where(f => f.Status == status);
        if (auditId.HasValue)                q = q.Where(f => f.InternalAuditId == auditId);
        return [.. q.OrderByDescending(f => f.CreatedAt)];
    }

    public List<AuditFinding> GetFindingsForUser(int userId, string role, string? category = null,
        string? severity = null, string? status = null, int? auditId = null)
    {
        var q = FindingQuery();
        if (!string.IsNullOrEmpty(category)) q = q.Where(f => f.Category == category);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(f => f.Severity == severity);
        if (!string.IsNullOrEmpty(status))   q = q.Where(f => f.Status == status);
        if (auditId.HasValue)                q = q.Where(f => f.InternalAuditId == auditId);
        q = ScopeFindings(q, userId, role);
        return [.. q.OrderByDescending(f => f.CreatedAt)];
    }

    public AuditFinding? GetFinding(int id) =>
        FindingQuery().FirstOrDefault(f => f.Id == id);

    public AuditFinding? GetFindingForUser(int id, int userId, string role)
    {
        var finding = GetFinding(id);
        return finding != null && CanAccessFinding(finding, userId, role) ? finding : null;
    }

    private IQueryable<AuditFinding> ScopeFindings(IQueryable<AuditFinding> q, int userId, string role)
    {
        if (role is "admin" or "audit_manager") return q;

        var userDeptIds = GetUserDepartmentIds(userId);

        if (role == "auditor")
            return q.Where(f => f.AuditorId == userId ||
                (f.InternalAudit != null && f.InternalAudit.LeadAuditorId == userId) ||
                (f.DepartmentId != null && userDeptIds.Contains(f.DepartmentId.Value)));

        if (role == "finding_owner")
            return q.Where(f => f.OwnerId == userId ||
                (f.DepartmentId != null && userDeptIds.Contains(f.DepartmentId.Value)));

        // risk_owner, risk_manager, committee — departmanlarındaki bulgular
        if (userDeptIds.Count > 0)
            return q.Where(f => f.DepartmentId != null && userDeptIds.Contains(f.DepartmentId.Value));

        return q.Where(_ => false);
    }

    public bool CanAccessFinding(AuditFinding finding, int userId, string role)
    {
        if (role is "admin" or "audit_manager") return true;
        if (finding.OwnerId == userId) return true;
        if (role == "auditor" && (finding.AuditorId == userId || finding.InternalAudit?.LeadAuditorId == userId))
            return true;
        if (finding.DepartmentId.HasValue)
        {
            var userDeptIds = GetUserDepartmentIds(userId);
            return userDeptIds.Contains(finding.DepartmentId.Value);
        }
        return false;
    }

    private HashSet<int> GetUserDepartmentIds(int userId)
    {
        var direct  = db.UserDepartments.Where(ud => ud.UserId == userId).Select(ud => ud.DepartmentId).ToHashSet();
        var primary = db.Users.Where(u => u.Id == userId && u.DepartmentId != null).Select(u => u.DepartmentId!.Value).FirstOrDefault();
        if (primary > 0) direct.Add(primary);
        var managed = db.Departments.Where(d => d.ManagerUserId == userId).Select(d => d.Id).ToHashSet();
        direct.UnionWith(managed);
        return direct;
    }

    public AuditFinding CreateFinding(string title, string? description,
        string? category, string? severity, string? auditPeriod,
        int auditorId, int? ownerId, int? internalAuditId, DateOnly? dueDate)
    {
        var finding = new AuditFinding
        {
            Code = GenerateFindingCode(),
            Title = title,
            Description = description,
            Category = category,
            Severity = severity,
            AuditPeriod = auditPeriod,
            AuditorId = auditorId,
            OwnerId = ownerId,
            InternalAuditId = internalAuditId,
            DueDate = dueDate,
        };
        db.AuditFindings.Add(finding);
        db.SaveChanges();
        return finding;
    }

    public bool UpdateFinding(int id, string title, string? description,
        string? category, string? severity, string? auditPeriod,
        int? ownerId, DateOnly? dueDate)
    {
        var f = db.AuditFindings.Find(id);
        if (f == null || f.Status == "closed") return false;
        f.Title = title;
        f.Description = description;
        f.Category = category;
        f.Severity = severity;
        f.AuditPeriod = auditPeriod;
        f.OwnerId = ownerId;
        f.DueDate = dueDate;
        db.SaveChanges();
        return true;
    }

    // ─── Closure Requests ─────────────────────────────────────────────────────

    public ClosureRequest SubmitClosureRequest(int findingId, string description,
        string? evidence, int requestedById)
    {
        var req = new ClosureRequest
        {
            FindingId = findingId,
            Description = description,
            Evidence = evidence,
            RequestedById = requestedById,
        };
        db.ClosureRequests.Add(req);
        var finding = db.AuditFindings.Find(findingId);
        if (finding != null) finding.Status = "closure_requested";
        db.SaveChanges();
        return req;
    }

    public bool ReviewClosureRequest(int findingId, int requestId,
        string decision, string? notes, int reviewedById)
    {
        var req = db.ClosureRequests
            .FirstOrDefault(r => r.Id == requestId && r.FindingId == findingId);
        if (req == null) return false;

        req.Status = decision; // approved | rejected
        req.ReviewNotes = notes;
        req.ReviewedById = reviewedById;
        req.ReviewedAt = DateTime.UtcNow;

        var finding = db.AuditFindings.Find(findingId);
        if (finding != null)
        {
            finding.Status = decision == "approved" ? "closed" : "open";
            if (decision == "approved") finding.ClosedAt = DateTime.UtcNow;
        }
        db.SaveChanges();
        return true;
    }

    // ─── Finding Actions ──────────────────────────────────────────────────────

    public AuditFindingAction AddFindingAction(int findingId, string description,
        string? responsible, DateOnly? dueDate, int createdById)
    {
        var action = new AuditFindingAction
        {
            FindingId = findingId,
            Description = description,
            Responsible = responsible,
            DueDate = dueDate,
            CreatedById = createdById,
        };
        db.AuditFindingActions.Add(action);
        var finding = db.AuditFindings.Find(findingId);
        if (finding != null) finding.ActionDecision = "action_planned";
        db.SaveChanges();
        return action;
    }

    public bool UpdateFindingActionStatus(int findingId, int actionId, string newStatus)
    {
        var action = db.AuditFindingActions.FirstOrDefault(a => a.Id == actionId && a.FindingId == findingId);
        if (action == null) return false;
        action.Status = newStatus;
        if (newStatus == "completed") action.CompletedAt = DateTime.UtcNow;
        db.SaveChanges();
        return true;
    }

    public bool DeleteFindingAction(int findingId, int actionId)
    {
        var action = db.AuditFindingActions.FirstOrDefault(a => a.Id == actionId && a.FindingId == findingId);
        if (action == null) return false;
        db.AuditFindingActions.Remove(action);
        db.SaveChanges();
        return true;
    }

    public bool SetActionDecision(int findingId, string? decision)
    {
        var finding = db.AuditFindings.Find(findingId);
        if (finding == null) return false;
        finding.ActionDecision = decision;
        db.SaveChanges();
        return true;
    }

    public List<AuditFindingAction> GetAllFindingActions()
    {
        return [.. db.AuditFindingActions
            .Include(a => a.Finding).ThenInclude(f => f.InternalAudit)
            .Include(a => a.CreatedBy)
            .OrderBy(a => a.DueDate == null).ThenBy(a => a.DueDate).ThenBy(a => a.Status)];
    }

    public List<AuditFindingAction> GetFindingActionsForUser(int userId, string role)
    {
        var q = db.AuditFindingActions
            .Include(a => a.Finding).ThenInclude(f => f.InternalAudit)
            .Include(a => a.CreatedBy)
            .AsQueryable();

        if (role is not ("admin" or "audit_manager"))
        {
            q = role switch
            {
                "auditor" => q.Where(a => a.Finding.AuditorId == userId ||
                    (a.Finding.InternalAudit != null && a.Finding.InternalAudit.LeadAuditorId == userId)),
                "finding_owner" => q.Where(a => a.Finding.OwnerId == userId),
                _ => q.Where(_ => false)
            };
        }

        return [.. q.OrderBy(a => a.DueDate == null).ThenBy(a => a.DueDate).ThenBy(a => a.Status)];
    }

    // ─── Attachments ──────────────────────────────────────────────────────────

    public FindingAttachment SaveAttachment(int findingId, string fileName, string storedPath, long fileSize, int uploadedById)
    {
        var att = new FindingAttachment
        {
            FindingId = findingId,
            FileName = fileName,
            StoredPath = storedPath,
            FileSize = fileSize,
            UploadedById = uploadedById,
        };
        db.FindingAttachments.Add(att);
        db.SaveChanges();
        return att;
    }

    public bool DeleteAttachment(int attachmentId)
    {
        var att = db.FindingAttachments.Find(attachmentId);
        if (att == null) return false;
        if (File.Exists(att.StoredPath)) File.Delete(att.StoredPath);
        db.FindingAttachments.Remove(att);
        db.SaveChanges();
        return true;
    }

    // ─── Dashboard ────────────────────────────────────────────────────────────

    public AuditDashboardStats GetDashboard(string[] severities)
    {
        var findings = db.AuditFindings.ToList();
        return BuildDashboardStats(findings, severities, db);
    }

    public AuditDashboardStats GetDashboardForUser(int userId, string role, string[] severities)
    {
        var findings = ScopeFindings(db.AuditFindings.Include(f => f.InternalAudit), userId, role).ToList();
        return BuildDashboardStats(findings, severities, db);
    }

    private static AuditDashboardStats BuildDashboardStats(List<AuditFinding> findings, string[] severities, AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var bySeverity = severities.ToDictionary(s => s, _ => 0);
        var byCategory = new Dictionary<string, int>();

        foreach (var f in findings)
        {
            if (f.Severity != null)
                bySeverity[f.Severity] = bySeverity.GetValueOrDefault(f.Severity) + 1;
            if (f.Category != null)
                byCategory[f.Category] = byCategory.GetValueOrDefault(f.Category) + 1;
        }

        // Plan istatistikleri — tablolar henüz yoksa sıfır döner
        var currentYear = DateTime.Today.Year;
        List<AuditPlanItem> planItems;
        HashSet<int> linkedAuditIds;
        try
        {
            planItems = db.AuditPlanItems.Where(i => i.Plan.Year == currentYear).ToList();
            linkedAuditIds = db.InternalAudits
                .Where(a => a.AuditPlanItemId != null)
                .Select(a => a.AuditPlanItemId!.Value)
                .ToHashSet();
        }
        catch
        {
            planItems      = [];
            linkedAuditIds = [];
        }

        return new AuditDashboardStats
        {
            Total            = findings.Count,
            Open             = findings.Count(f => f.Status == "open"),
            ClosureRequested = findings.Count(f => f.Status == "closure_requested"),
            Closed           = findings.Count(f => f.Status == "closed"),
            Overdue          = findings.Count(f => f.Status != "closed" && f.DueDate.HasValue && f.DueDate < today),
            BySeverity       = bySeverity,
            ByCategory       = byCategory,
            // Aktif denetimler
            ActiveAudits     = db.InternalAudits.Count(a => a.Status == "in_progress"),
            PlannedAudits    = db.InternalAudits.Count(a => a.Status == "planned"),
            CompletedAudits  = db.InternalAudits.Count(a => a.Status == "completed"),
            // Plan tamamlanma
            PlanYear         = currentYear,
            PlanTotal        = planItems.Count,
            PlanConverted    = planItems.Count(i => linkedAuditIds.Contains(i.Id)),
            PlanCompleted    = planItems.Count(i => GetItemStatus(i) is "completed" or "completed_late"),
            PlanLate         = planItems.Count(i => GetItemStatus(i) == "late"),
        };
    }

    // ─── Dosya servisi ────────────────────────────────────────────────────────

    public static (string? StoredPath, string FileName)? ResolveAttachmentPath(
        FindingAttachment attachment, string contentRootPath, string? webRootPath)
    {
        var uploadsRoot  = Path.GetFullPath(Path.Combine(contentRootPath, "uploads", "findings"));
        var legacyRoot   = string.IsNullOrEmpty(webRootPath) ? null
                           : Path.GetFullPath(Path.Combine(webRootPath, "uploads", "findings"));
        var storedPath   = Path.GetFullPath(attachment.StoredPath);
        var isValid      = storedPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        || (legacyRoot != null && storedPath.StartsWith(legacyRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        return isValid && File.Exists(storedPath) ? (storedPath, attachment.FileName) : null;
    }

    public static (string? StoredPath, string FileName)? ResolveClosureFilePath(
        int findingId, string fileName, string contentRootPath, string? webRootPath)
    {
        var safeName    = Path.GetFileName(fileName);
        if (!string.Equals(fileName, safeName, StringComparison.Ordinal)) return null;
        var uploadsRoot = Path.GetFullPath(Path.Combine(contentRootPath, "uploads", "findings"));
        var legacyRoot  = string.IsNullOrEmpty(webRootPath) ? null
                          : Path.GetFullPath(Path.Combine(webRootPath, "uploads", "findings"));

        var path = Path.GetFullPath(Path.Combine(contentRootPath, "uploads", "findings", findingId.ToString(), "closure", safeName));
        if (!File.Exists(path) && webRootPath != null)
            path = Path.GetFullPath(Path.Combine(webRootPath, "uploads", "findings", findingId.ToString(), "closure", safeName));

        var isValid = path.StartsWith(uploadsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                   || (legacyRoot != null && path.StartsWith(legacyRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        return isValid && File.Exists(path) ? (path, safeName) : null;
    }

    // ─── Dosya erişim yetkisi — Program.cs'ten buraya taşındı ────────────────
    public static bool CanDownloadFindingFile(ClaimsPrincipal user, AuditFinding finding)
    {
        // Tüm rol claim'lerini kontrol et (multi-role kullanıcı desteği)
        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
        if (roles.Contains("admin") || roles.Contains("audit_manager")) return true;

        if (!int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return false;

        return (roles.Contains("auditor") &&
                   (finding.AuditorId == userId || finding.InternalAudit?.LeadAuditorId == userId)) ||
               (roles.Contains("finding_owner") && finding.OwnerId == userId);
    }
}

public record AuditDashboardStats
{
    // Bulgular
    public int Total            { get; init; }
    public int Open             { get; init; }
    public int ClosureRequested { get; init; }
    public int Closed           { get; init; }
    public int Overdue          { get; init; }
    public Dictionary<string, int> BySeverity { get; init; } = [];
    public Dictionary<string, int> ByCategory { get; init; } = [];
    // İç denetimler
    public int ActiveAudits   { get; init; }
    public int PlannedAudits  { get; init; }
    public int CompletedAudits{ get; init; }
    // Yıllık plan
    public int PlanYear      { get; init; }
    public int PlanTotal     { get; init; }
    public int PlanConverted { get; init; }
    public int PlanCompleted { get; init; }
    public int PlanLate      { get; init; }
}
