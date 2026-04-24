using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuditService(AppDbContext db)
{
    // ─── Internal Audits ──────────────────────────────────────────────────────

    public string GenerateAuditCode()
    {
        var year = DateTime.UtcNow.Year;
        var count = db.InternalAudits.Count(a => a.Code.StartsWith($"ID-{year}-"));
        return $"ID-{year}-{(count + 1):D3}";
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

    public InternalAudit? GetAudit(int id) =>
        AuditQuery().FirstOrDefault(a => a.Id == id);

    public InternalAudit CreateAudit(string title, string? auditType, string? scope,
        string period, DateOnly? startDate, DateOnly? endDate, int leadAuditorId)
    {
        var audit = new InternalAudit
        {
            Code = GenerateAuditCode(),
            Title = title,
            AuditType = auditType,
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

    public bool UpdateAudit(int id, string title, string? auditType, string? scope,
        string period, DateOnly? startDate, DateOnly? endDate, string status)
    {
        var audit = db.InternalAudits.Find(id);
        if (audit == null) return false;
        audit.Title = title;
        audit.AuditType = auditType;
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
        var count = db.AuditFindings.Count(f => f.Code.StartsWith($"B-{year}-"));
        return $"B-{year}-{(count + 1):D3}";
    }

    public IQueryable<AuditFinding> FindingQuery() => db.AuditFindings
        .Include(f => f.Auditor)
        .Include(f => f.Owner)
        .Include(f => f.InternalAudit)
        .Include(f => f.ClosureRequests).ThenInclude(c => c.RequestedBy)
        .Include(f => f.ClosureRequests).ThenInclude(c => c.ReviewedBy);

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

    public AuditFinding? GetFinding(int id) =>
        FindingQuery().FirstOrDefault(f => f.Id == id);

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

    // ─── Dashboard ────────────────────────────────────────────────────────────

    public AuditDashboardStats GetDashboard(string[] severities)
    {
        var findings = db.AuditFindings.ToList();
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
