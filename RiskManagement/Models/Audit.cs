namespace RiskManagement.Models;

public class InternalAudit
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Scope { get; set; }
    public string Period { get; set; } = "";
    public string? AuditType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string Status { get; set; } = "planned"; // planned|in_progress|completed
    public int LeadAuditorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User LeadAuditor { get; set; } = null!;
    public ICollection<AuditFinding> Findings { get; set; } = [];
}

public class AuditFinding
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Severity { get; set; }
    public string? AuditPeriod { get; set; }
    public int AuditorId { get; set; }
    public int? OwnerId { get; set; }
    public int? InternalAuditId { get; set; }
    public string Status { get; set; } = "open"; // open|closure_requested|closed
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public User Auditor { get; set; } = null!;
    public User? Owner { get; set; }
    public InternalAudit? InternalAudit { get; set; }
    public ICollection<ClosureRequest> ClosureRequests { get; set; } = [];
}

public class ClosureRequest
{
    public int Id { get; set; }
    public int FindingId { get; set; }
    public string Description { get; set; } = "";
    public string? Evidence { get; set; }
    public int RequestedById { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "pending"; // pending|approved|rejected
    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
