using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuditService(AppDbContext db, INotificationService? notifications = null, AuthService? auth = null,
    ILogger<AuditService>? logger = null)
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

    public async Task<AuditPlan> EnsurePlanAsync(int year, string title, int userId)
    {
        var plan = db.AuditPlans.FirstOrDefault(p => p.Year == year);
        if (plan != null) return plan;
        plan = new AuditPlan { Year = year, Title = title, CreatedById = userId };
        db.AuditPlans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    public async Task UpdatePlanAsync(AuditPlan plan)
    {
        db.AuditPlans.Update(plan);
        await db.SaveChangesAsync();
    }

    public async Task<AuditPlanItem> AddPlanItemAsync(AuditPlanItem item)
    {
        var maxOrder = db.AuditPlanItems
            .Where(i => i.AuditPlanId == item.AuditPlanId)
            .Select(i => (int?)i.SortOrder).Max() ?? 0;
        item.SortOrder = maxOrder + 1;
        db.AuditPlanItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdatePlanItemAsync(AuditPlanItem item)
    {
        db.AuditPlanItems.Update(item);
        await db.SaveChangesAsync();
    }

    public async Task DeletePlanItemAsync(int itemId)
    {
        var item = await db.AuditPlanItems.FindAsync(itemId);
        if (item is null) return;
        db.AuditPlanItems.Remove(item);
        await db.SaveChangesAsync();
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

    public List<InternalAudit> GetAuditsForUser(User user, string? statusFilter = null)
    {
        var q = AuditQuery();
        if (!string.IsNullOrEmpty(statusFilter)) q = q.Where(a => a.Status == statusFilter);
        q = ScopeAuditsForUser(q, user);
        return [.. q.OrderByDescending(a => a.CreatedAt)];
    }

    private IQueryable<InternalAudit> ScopeAuditsForUser(IQueryable<InternalAudit> q, User user)
    {
        if (user.HasAnyRole("admin", "audit_manager")) return q;
        var allRoles = user.AllRoleNames.ToHashSet();
        var userId   = user.Id;
        var deptIds  = user.AllDepartmentIds.ToHashSet();
        return q.Where(a =>
            (allRoles.Contains("auditor") && (
                a.LeadAuditorId == userId ||
                a.Findings.Any(f => f.AuditorId == userId) ||
                (a.DepartmentId != null && deptIds.Contains(a.DepartmentId.Value)))) ||
            (allRoles.Contains("finding_owner") && a.Findings.Any(f => f.OwnerId == userId))
        );
    }

    public InternalAudit? GetAudit(int id) =>
        AuditQueryFull().FirstOrDefault(a => a.Id == id);

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
        if (role == "finding_owner")
            return q.Where(a => a.Findings.Any(f => f.OwnerId == userId));
        return q.Where(_ => false);
    }

    public bool CanAccessAudit(InternalAudit audit, int userId, string role)
    {
        if (role is "admin" or "audit_manager") return true;
        if (role == "auditor" && (audit.LeadAuditorId == userId || audit.Findings.Any(f => f.AuditorId == userId)))
            return true;
        if (role == "finding_owner" && audit.Findings.Any(f => f.OwnerId == userId))
            return true;
        return false;
    }

    public async Task<InternalAudit> CreateAuditAsync(string title, string? auditType, string? auditedUnit,
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
        await db.SaveChangesAsync();
        return audit;
    }

    /// <summary>
    /// Denetim planındaki bir maddeden otomatik iç denetim oluşturur.
    /// Madde zaten bir denetime bağlıysa hata fırlatır.
    /// </summary>
    public async Task<InternalAudit> CreateAuditFromPlanItemAsync(int planItemId, int leadAuditorId)
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

        return await CreateAuditAsync(
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
    }

    public async Task<bool> UpdateAuditAsync(int id, string title, string? auditType, string? auditedUnit,
        string? scope, string period, DateOnly? startDate, DateOnly? endDate, string status,
        int? departmentId = null)
    {
        var audit = await db.InternalAudits.FindAsync(id);
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
        await db.SaveChangesAsync();

        await SyncPlanItemDatesAsync(audit);

        return true;
    }

    private async Task SyncPlanItemDatesAsync(InternalAudit audit)
    {
        if (audit.AuditPlanItemId == null) return;
        var item = await db.AuditPlanItems.FindAsync(audit.AuditPlanItemId);
        if (item == null) return;

        var today = DateOnly.FromDateTime(DateTime.Today);

        if (audit.Status == AuditStatus.InProgress && item.ActualStartDate == null)
            item.ActualStartDate = audit.StartDate ?? today;

        if (audit.Status == AuditStatus.Completed && item.ActualEndDate == null)
        {
            if (item.ActualStartDate == null)
                item.ActualStartDate = audit.StartDate ?? today;
            item.ActualEndDate = audit.EndDate ?? today;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Plan maddesine bağlı InternalAudit'i döner (yoksa null).</summary>
    public InternalAudit? GetAuditByPlanItem(int planItemId) =>
        AuditQuery().FirstOrDefault(a => a.AuditPlanItemId == planItemId);

    public Dictionary<int, InternalAudit> GetAuditsByPlanItems(IEnumerable<int> planItemIds)
    {
        var ids = planItemIds.ToHashSet();
        return db.InternalAudits
            .Where(a => a.AuditPlanItemId != null && ids.Contains(a.AuditPlanItemId!.Value))
            .ToDictionary(a => a.AuditPlanItemId!.Value, a => a);
    }

    // ─── Findings ─────────────────────────────────────────────────────────────

    public string GenerateFindingCode()
    {
        var year = DateTime.UtcNow.Year;
        return $"B-{year}-{CounterHelper.GetNext(db, $"finding-{year}"):D3}";
    }

    private void LogFinding(int findingId, int? userId, string action, string? detail = null)
    {
        db.FindingActivityLogs.Add(new RiskManagement.Models.FindingActivityLog
        {
            FindingId = findingId, UserId = userId,
            Action = action, Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Tam sorgu — detay sayfası için tüm navigasyon property'leri yükler.
    /// Liste/export kullanım alanları için FindingListQuery() tercih edin.
    /// </summary>
    public IQueryable<AuditFinding> FindingQuery() => db.AuditFindings
        .AsNoTracking()
        .Include(f => f.Auditor)
        .Include(f => f.Owner)
        .Include(f => f.Department)
        .Include(f => f.InternalAudit)
        .Include(f => f.ClosureRequests).ThenInclude(c => c.RequestedBy)
        .Include(f => f.ClosureRequests).ThenInclude(c => c.ReviewedBy)
        .Include(f => f.Actions).ThenInclude(a => a.CreatedBy)
        .Include(f => f.Attachments).ThenInclude(a => a.UploadedBy)
        .Include(f => f.ActivityLogs).ThenInclude(l => l.User)
        .Include(f => f.RiskLinks).ThenInclude(l => l.Risk);

    /// <summary>
    /// Hafif liste sorgusu — kart/tablo görünümü için yeterli, 7 Include yerine 4.
    /// Closure/attachment/log/riskLink verisi gerektiğinde FindingQuery() kullanın.
    /// </summary>
    public IQueryable<AuditFinding> FindingListQuery() => db.AuditFindings
        .AsNoTracking()
        .Include(f => f.Auditor)
        .Include(f => f.Owner)
        .Include(f => f.Department)
        .Include(f => f.InternalAudit);

    public List<AuditFinding> GetFindings(string? category = null, string? severity = null,
        string? status = null, int? auditId = null)
    {
        // Liste görünümü — tam FindingQuery() yerine hafif sorgu kullanılır.
        var q = FindingListQuery().Where(f => f.AuditSource == "internal");
        if (!string.IsNullOrEmpty(category)) q = q.Where(f => f.Category == category);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(f => f.Severity == severity);
        if (!string.IsNullOrEmpty(status))   q = q.Where(f => f.Status == status);
        if (auditId.HasValue)                q = q.Where(f => f.InternalAuditId == auditId);
        return [.. q.OrderByDescending(f => f.CreatedAt)];
    }

    public List<AuditFinding> GetFindingsForUser(int userId, string role, string? category = null,
        string? severity = null, string? status = null, int? auditId = null)
    {
        // Liste/export — hafif sorgu; ClosureRequest/Attachment/Log/RiskLink yüklenmez.
        var q = FindingListQuery().Where(f => f.AuditSource == "internal");
        if (!string.IsNullOrEmpty(category)) q = q.Where(f => f.Category == category);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(f => f.Severity == severity);
        if (!string.IsNullOrEmpty(status))   q = q.Where(f => f.Status == status);
        if (auditId.HasValue)                q = q.Where(f => f.InternalAuditId == auditId);
        q = ScopeFindings(q, userId, role);
        return [.. q.OrderByDescending(f => f.CreatedAt)];
    }

    // Tüm rolleri ve departman atamalarını kullanan overload (DB'den tam yüklü User için)
    public List<AuditFinding> GetFindingsForUser(User user, string? category = null,
        string? severity = null, string? status = null, int? auditId = null)
    {
        // Liste/export — hafif sorgu.
        var q = FindingListQuery().Where(f => f.AuditSource == "internal");
        if (!string.IsNullOrEmpty(category)) q = q.Where(f => f.Category == category);
        if (!string.IsNullOrEmpty(severity)) q = q.Where(f => f.Severity == severity);
        if (!string.IsNullOrEmpty(status))   q = q.Where(f => f.Status == status);
        if (auditId.HasValue)                q = q.Where(f => f.InternalAuditId == auditId);
        q = ScopeFindingsForUser(q, user);
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

    // Tüm rolleri birleştiren versiyon — çok rollü kullanıcılarda eksik erişimi önler
    private IQueryable<AuditFinding> ScopeFindingsForUser(IQueryable<AuditFinding> q, User user)
    {
        if (user.HasAnyRole("admin", "audit_manager")) return q;

        var deptIds  = user.AllDepartmentIds.ToHashSet();
        var allRoles = user.AllRoleNames.ToHashSet();
        var userId   = user.Id;

        return q.Where(f =>
            (allRoles.Contains("auditor") && (
                f.AuditorId == userId ||
                (f.InternalAudit != null && f.InternalAudit.LeadAuditorId == userId) ||
                (f.DepartmentId != null && deptIds.Contains(f.DepartmentId.Value)))) ||
            (allRoles.Contains("finding_owner") && (
                f.OwnerId == userId ||
                (f.DepartmentId != null && deptIds.Contains(f.DepartmentId.Value)))) ||
            (deptIds.Count > 0 && f.DepartmentId != null && deptIds.Contains(f.DepartmentId.Value))
        );
    }

    /// <summary>
    /// Bulgu <b>görüntüleme</b> yetkisi (detay sayfası, liste). Departman üyelerine ve
    /// rolden bağımsız atanan sahibe görünürlük verir. Dikkat: ek <b>indirme</b> yetkisi
    /// bilinçli olarak daha dardır — bkz. <see cref="CanDownloadFindingFile"/>. Bir bulguyu
    /// görebilen herkes eklerini indiremeyebilir (kanıt dosyaları daha hassas kabul edilir).
    /// </summary>
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

    public async Task<AuditFinding> CreateFindingAsync(string title, string? description,
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

        await using var tx = await db.Database.BeginTransactionAsync();
        db.AuditFindings.Add(finding);
        await db.SaveChangesAsync();
        LogFinding(finding.Id, auditorId, "Bulgu Oluşturuldu", title);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        if (ownerId.HasValue)
            _ = notifications?.NotifyFindingAssignedAsync(finding.Code, title, ownerId.Value);
        return finding;
    }

    public async Task<bool> UpdateFindingAsync(int id, string title, string? description,
        string? category, string? severity, string? auditPeriod,
        int? ownerId, DateOnly? dueDate, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return false;
        return await InternalUpdateFindingAsync(id, title, description, category, severity, auditPeriod, ownerId, dueDate);
    }

    internal async Task<bool> InternalUpdateFindingAsync(int id, string title, string? description,
        string? category, string? severity, string? auditPeriod,
        int? ownerId, DateOnly? dueDate)
    {
        var f = await db.AuditFindings.FindAsync(id);
        if (f == null || f.Status == FindingStatus.Closed) return false;
        f.Title = title;
        f.Description = description;
        f.Category = category;
        f.Severity = severity;
        f.AuditPeriod = auditPeriod;
        var ownerChanged = f.OwnerId != ownerId;
        f.OwnerId = ownerId;
        f.DueDate = dueDate;
        LogFinding(id, ownerId, "Bulgu Güncellendi", title);
        await db.SaveChangesAsync();
        if (ownerChanged && ownerId.HasValue)
            notifications?.NotifyFindingAssignedAsync(f.Code, title, ownerId.Value);
        return true;
    }

    // ─── Closure Requests ─────────────────────────────────────────────────────

    public async Task<ClosureRequest?> SubmitClosureRequestAsync(int findingId, string description,
        string? evidence, int requestedById, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return null;
        return await InternalSubmitClosureRequestAsync(findingId, description, evidence, requestedById);
    }

    internal async Task<ClosureRequest?> InternalSubmitClosureRequestAsync(int findingId, string description,
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
        var finding = await db.AuditFindings.FindAsync(findingId);
        if (finding != null) finding.Status = FindingStatus.ClosureRequested;
        LogFinding(findingId, requestedById, "Kapanış Başvurusu Yapıldı");
        await db.SaveChangesAsync();
        if (finding != null)
            _ = notifications?.NotifyClosureRequestedAsync(finding.Code, finding.Title, finding.AuditorId);
        return req;
    }

    public async Task<bool> ReviewClosureRequestAsync(int findingId, int requestId,
        string decision, string? notes, int reviewedById, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.close_approve")) return false;
        return await InternalReviewClosureRequestAsync(findingId, requestId, decision, notes, reviewedById);
    }

    internal async Task<bool> InternalReviewClosureRequestAsync(int findingId, int requestId,
        string decision, string? notes, int reviewedById)
    {
        if (decision is not ("approved" or "rejected"))
        {
            logger?.LogWarning("Kapanış değerlendirme reddi — geçersiz karar: {Decision}, FindingId: {FindingId}, Kullanıcı: {UserId}",
                decision, findingId, reviewedById);
            return false;
        }

        var req = db.ClosureRequests
            .FirstOrDefault(r => r.Id == requestId && r.FindingId == findingId);
        if (req == null) return false;

        req.Status = decision;
        req.ReviewNotes = notes;
        req.ReviewedById = reviewedById;
        req.ReviewedAt = DateTime.UtcNow;

        var finding = await db.AuditFindings.FindAsync(findingId);
        if (finding != null)
        {
            finding.Status = decision == "approved" ? FindingStatus.Closed : FindingStatus.Open;
            if (decision == "approved") finding.ClosedAt = DateTime.UtcNow;
        }
        var actionLabel = decision == "approved" ? "Bulgu Kapatıldı" : "Kapanış Başvurusu Reddedildi";
        LogFinding(findingId, reviewedById, actionLabel, notes);
        await db.SaveChangesAsync();
        if (finding?.OwnerId.HasValue == true)
            _ = notifications?.NotifyClosureDecidedAsync(finding.Code, finding.Title, decision, finding.OwnerId.Value);
        return true;
    }

    // ─── Finding Actions ──────────────────────────────────────────────────────

    public async Task<AuditFindingAction?> AddFindingActionAsync(int findingId, string description,
        string? responsible, DateOnly? dueDate, int createdById, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return null;
        return await InternalAddFindingActionAsync(findingId, description, responsible, dueDate, createdById);
    }

    internal async Task<AuditFindingAction> InternalAddFindingActionAsync(int findingId, string description,
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
        var finding = await db.AuditFindings.FindAsync(findingId);
        if (finding != null) finding.ActionDecision = "action_planned";
        LogFinding(findingId, createdById, "Aksiyon Planı Eklendi", description.Length > 80 ? description[..80] + "…" : description);
        await db.SaveChangesAsync();
        return action;
    }

    public async Task<bool> UpdateFindingActionAsync(int findingId, int actionId,
        string description, string? responsible, DateOnly? dueDate, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return false;
        return await InternalUpdateFindingActionAsync(findingId, actionId, description, responsible, dueDate);
    }

    internal async Task<bool> InternalUpdateFindingActionAsync(int findingId, int actionId,
        string description, string? responsible, DateOnly? dueDate)
    {
        var action = db.AuditFindingActions
            .FirstOrDefault(a => a.Id == actionId && a.FindingId == findingId);
        if (action == null) return false;
        action.Description = description;
        action.Responsible  = responsible;
        action.DueDate      = dueDate;
        LogFinding(findingId, action.CreatedById, "Aksiyon Güncellendi",
            description.Length > 80 ? description[..80] + "…" : description);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateFindingActionStatusAsync(int findingId, int actionId, string newStatus, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return false;
        return await InternalUpdateFindingActionStatusAsync(findingId, actionId, newStatus);
    }

    internal async Task<bool> InternalUpdateFindingActionStatusAsync(int findingId, int actionId, string newStatus)
    {
        var action = db.AuditFindingActions.FirstOrDefault(a => a.Id == actionId && a.FindingId == findingId);
        if (action == null) return false;
        var oldStatus = action.Status;
        action.Status = newStatus;
        if (newStatus == "completed") action.CompletedAt = DateTime.UtcNow;
        LogFinding(findingId, action.CreatedById, "Aksiyon Durumu Güncellendi", $"{ActionStatusLabel(oldStatus)} → {ActionStatusLabel(newStatus)}");
        await db.SaveChangesAsync();
        return true;
    }

    private static string ActionStatusLabel(string s) => s switch
    {
        "planned" => "Planlandı", "in_progress" => "Devam Ediyor",
        "completed" => "Tamamlandı", "cancelled" => "İptal", _ => s
    };

    public async Task<bool> DeleteFindingActionAsync(int findingId, int actionId, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return false;
        return await InternalDeleteFindingActionAsync(findingId, actionId);
    }

    internal async Task<bool> InternalDeleteFindingActionAsync(int findingId, int actionId)
    {
        var action = db.AuditFindingActions.FirstOrDefault(a => a.Id == actionId && a.FindingId == findingId);
        if (action == null) return false;
        db.AuditFindingActions.Remove(action);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> SetActionDecisionAsync(int findingId, string? decision, User caller)
    {
        if (auth is null) throw new InvalidOperationException("AuditService: AuthService inject edilmedi");
        if (!auth.HasPermission(caller, "audit.modify")) return false;
        return await InternalSetActionDecisionAsync(findingId, decision);
    }

    internal async Task<bool> InternalSetActionDecisionAsync(int findingId, string? decision)
    {
        var finding = await db.AuditFindings.FindAsync(findingId);
        if (finding == null) return false;
        finding.ActionDecision = decision;
        await db.SaveChangesAsync();
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
            .Where(a => a.Finding.AuditSource == "internal")
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

    public List<AuditFindingAction> GetFindingActionsForUser(User user)
    {
        var q = db.AuditFindingActions
            .Include(a => a.Finding).ThenInclude(f => f.InternalAudit)
            .Include(a => a.CreatedBy)
            .Where(a => a.Finding.AuditSource == "internal")
            .AsQueryable();

        if (!user.HasAnyRole("admin", "audit_manager"))
        {
            var allRoles = user.AllRoleNames.ToHashSet();
            var userId   = user.Id;
            var deptIds  = user.AllDepartmentIds.ToHashSet();
            q = q.Where(a =>
                (allRoles.Contains("auditor") && (
                    a.Finding.AuditorId == userId ||
                    (a.Finding.InternalAudit != null && a.Finding.InternalAudit.LeadAuditorId == userId) ||
                    (a.Finding.DepartmentId != null && deptIds.Contains(a.Finding.DepartmentId.Value)))) ||
                (allRoles.Contains("finding_owner") && (
                    a.Finding.OwnerId == userId ||
                    (a.Finding.DepartmentId != null && deptIds.Contains(a.Finding.DepartmentId.Value))))
            );
        }

        return [.. q.OrderBy(a => a.DueDate == null).ThenBy(a => a.DueDate).ThenBy(a => a.Status)];
    }

    // ─── Attachments ──────────────────────────────────────────────────────────

    public async Task<FindingAttachment> SaveAttachmentAsync(int findingId, string fileName, string storedPath, long fileSize, int uploadedById)
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
        await db.SaveChangesAsync();
        return att;
    }

    public async Task<bool> DeleteAttachmentAsync(int attachmentId)
    {
        var att = await db.FindingAttachments.FindAsync(attachmentId);
        if (att == null) return false;
        if (File.Exists(att.StoredPath)) File.Delete(att.StoredPath);
        db.FindingAttachments.Remove(att);
        await db.SaveChangesAsync();
        return true;
    }

    // ─── Dashboard ────────────────────────────────────────────────────────────

    public AuditDashboardStats GetDashboard(string[] severities)
    {
        // Dashboard yalnızca sayım/gruplandırma yapar — navigasyon property gereksiz, AsNoTracking yeterli.
        var baseQ = db.AuditFindings.AsNoTracking().Where(f => f.AuditSource == "internal");
        return ComputeDashboardStats(baseQ, severities);
    }

    public AuditDashboardStats GetDashboardForUser(int userId, string role, string[] severities)
    {
        var baseQ = ScopeFindings(
            db.AuditFindings.AsNoTracking().Where(f => f.AuditSource == "internal"),
            userId, role);
        IQueryable<InternalAudit>? auditScope = (role is "admin" or "audit_manager") ? null
            : db.InternalAudits.Where(a =>
                a.LeadAuditorId == userId ||
                a.Findings.Any(f => f.AuditorId == userId || f.OwnerId == userId));
        return ComputeDashboardStats(baseQ, severities, auditScope);
    }

    public AuditDashboardStats GetDashboardForUser(User user, string[] severities)
    {
        var baseQ = ScopeFindingsForUser(
            db.AuditFindings.AsNoTracking().Where(f => f.AuditSource == "internal"),
            user);
        IQueryable<InternalAudit>? auditScope = user.HasAnyRole("admin", "audit_manager") ? null
            : db.InternalAudits.Where(a =>
                a.LeadAuditorId == user.Id ||
                a.Findings.Any(f => f.AuditorId == user.Id || f.OwnerId == user.Id));
        return ComputeDashboardStats(baseQ, severities, auditScope);
    }

    private AuditDashboardStats ComputeDashboardStats(IQueryable<AuditFinding> baseQ, string[] severities,
        IQueryable<InternalAudit>? auditScope = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Status sayımları — server-side GroupBy
        var statusCounts = baseQ
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionary(x => x.Status, x => x.Count);

        int GetStatus(string key) => statusCounts.GetValueOrDefault(key, 0);

        // Gecikmiş — server-side
        var overdue = baseQ.Count(f => f.Status != FindingStatus.Closed && f.DueDate.HasValue && f.DueDate < today);

        // Şiddet dağılımı — server-side GroupBy
        var bySeverity = severities.ToDictionary(s => s, _ => 0);
        baseQ.Where(f => f.Severity != null)
             .GroupBy(f => f.Severity!)
             .Select(g => new { Sev = g.Key, Count = g.Count() })
             .ToList()
             .ForEach(x => { if (bySeverity.ContainsKey(x.Sev)) bySeverity[x.Sev] = x.Count; });

        // Kategori dağılımı — server-side GroupBy
        var byCategory = baseQ.Where(f => f.Category != null)
            .GroupBy(f => f.Category!)
            .Select(g => new { Cat = g.Key, Count = g.Count() })
            .ToDictionary(x => x.Cat, x => x.Count);

        // Plan istatistikleri — GetItemStatus() client-side hesaplama gerektiriyor
        var currentYear = DateTime.Today.Year;
        var planItems = db.AuditPlanItems
            .Include(i => i.Plan)
            .Where(i => i.Plan.Year == currentYear)
            .ToList();
        var linkedAuditIds = db.InternalAudits
            .Where(a => a.AuditPlanItemId != null)
            .Select(a => a.AuditPlanItemId!.Value)
            .ToHashSet();

        return new AuditDashboardStats
        {
            Total            = statusCounts.Values.Sum(),
            Open             = GetStatus("open"),
            ClosureRequested = GetStatus("closure_requested"),
            Closed           = GetStatus("closed"),
            Overdue          = overdue,
            BySeverity       = bySeverity,
            ByCategory       = byCategory,
            ActiveAudits     = (auditScope ?? db.InternalAudits).Count(a => a.Status == AuditStatus.InProgress),
            PlannedAudits    = (auditScope ?? db.InternalAudits).Count(a => a.Status == AuditStatus.Planned),
            CompletedAudits  = (auditScope ?? db.InternalAudits).Count(a => a.Status == AuditStatus.Completed),
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
    /// <summary>
    /// Bulgu eki <b>indirme</b> yetkisi. <see cref="CanAccessFinding"/>'ten <b>kasıtlı olarak daha katı</b>:
    /// departman üyeliği veya rolsüz sahiplik yeterli değildir. Yalnızca admin/audit_manager,
    /// bulgunun denetçisi (veya iç denetimin baş denetçisi) ve <c>finding_owner</c> rolündeki
    /// atanan sahip dosyayı indirebilir. Gerekçe: ek/kapanış dosyaları hassas denetim kanıtıdır;
    /// metadata görünürlüğü, kanıt erişiminden daha geniş tutulur. Bu fark kaldırılacaksa
    /// <see cref="CanAccessFinding"/> ile hizalanmalı ve ilgili pinleme testi güncellenmelidir.
    /// </summary>
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
