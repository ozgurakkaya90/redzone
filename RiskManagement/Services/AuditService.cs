using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuditService(AppDbContext db)
{
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

    public IQueryable<InternalAudit> AuditQuery() => db.InternalAudits
        .Include(a => a.LeadAuditor)
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

    public InternalAudit? GetAudit(int id) =>
        AuditQuery().FirstOrDefault(a => a.Id == id);

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
        string? scope, string period, DateOnly? startDate, DateOnly? endDate, int leadAuditorId)
    {
        var audit = new InternalAudit
        {
            Code = GenerateAuditCode(),
            Title = title,
            AuditType = auditType,
            AuditedUnit = auditedUnit,
            Scope = scope,
            Period = period,
            StartDate = startDate,
            EndDate = endDate,
            LeadAuditorId = leadAuditorId,
        };
        db.InternalAudits.Add(audit);
        db.SaveChanges();
        return audit;
    }

    public bool UpdateAudit(int id, string title, string? auditType, string? auditedUnit,
        string? scope, string period, DateOnly? startDate, DateOnly? endDate, string status)
    {
        var audit = db.InternalAudits.Find(id);
        if (audit == null) return false;
        audit.Title = title;
        audit.AuditType = auditType;
        audit.AuditedUnit = auditedUnit;
        audit.Scope = scope;
        audit.Period = period;
        audit.StartDate = startDate;
        audit.EndDate = endDate;
        audit.Status = status;
        db.SaveChanges();
        return true;
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
        return BuildDashboardStats(findings, severities);
    }

    public AuditDashboardStats GetDashboardForUser(int userId, string role, string[] severities)
    {
        var findings = ScopeFindings(db.AuditFindings.Include(f => f.InternalAudit), userId, role).ToList();
        return BuildDashboardStats(findings, severities);
    }

    private static AuditDashboardStats BuildDashboardStats(List<AuditFinding> findings, string[] severities)
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

        return new AuditDashboardStats
        {
            Total = findings.Count,
            Open = findings.Count(f => f.Status == "open"),
            ClosureRequested = findings.Count(f => f.Status == "closure_requested"),
            Closed = findings.Count(f => f.Status == "closed"),
            Overdue = findings.Count(f => f.Status != "closed" && f.DueDate.HasValue && f.DueDate < today),
            BySeverity = bySeverity,
            ByCategory = byCategory,
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
    public int Total { get; init; }
    public int Open { get; init; }
    public int ClosureRequested { get; init; }
    public int Closed { get; init; }
    public int Overdue { get; init; }
    public Dictionary<string, int> BySeverity { get; init; } = [];
    public Dictionary<string, int> ByCategory { get; init; } = [];
}
