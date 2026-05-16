using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class AuditPlan
{
    public int Id { get; set; }
    public int Year { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User CreatedBy { get; set; } = null!;
    public ICollection<AuditPlanItem> Items { get; set; } = [];
}

public class AuditPlanItem
{
    public int Id { get; set; }
    public int AuditPlanId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(100)]
    public string? AuditType { get; set; }

    [MaxLength(200)]
    public string? AuditedUnit { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateOnly PlannedStartDate { get; set; }
    public DateOnly PlannedEndDate { get; set; }

    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? ActualEndDate { get; set; }

    public int? DepartmentId { get; set; }

    public int? ResponsibleId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public AuditPlan Plan { get; set; } = null!;
    public Department? Department { get; set; }
    public User? Responsible { get; set; }
}

public class InternalAudit
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = "";

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Scope { get; set; }

    [Required, MaxLength(20)]
    public string Period { get; set; } = "";

    [MaxLength(50)]
    public string? AuditType { get; set; }

    [MaxLength(200)]
    public string? AuditedUnit { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "planned"; // planned|in_progress|completed

    public int? DepartmentId { get; set; }

    /// <summary>Hangi plan maddesinden oluşturuldu (varsa)</summary>
    public int? AuditPlanItemId { get; set; }

    public int LeadAuditorId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User LeadAuditor { get; set; } = null!;
    public Department? Department { get; set; }
    public AuditPlanItem? AuditPlanItem { get; set; }
    public ICollection<AuditFinding> Findings { get; set; } = [];
}

public class AuditFinding
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string Code { get; set; } = "";

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(20)]
    public string? Severity { get; set; }

    [MaxLength(20)]
    public string? AuditPeriod { get; set; }

    public int AuditorId { get; set; }
    public int? OwnerId { get; set; }
    public int? DepartmentId { get; set; }
    public int? InternalAuditId { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "open"; // open|closure_requested|closed

    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    [MaxLength(20)]
    public string? ActionDecision { get; set; } // null | "action_planned" | "risk_accepted"

    public User Auditor { get; set; } = null!;
    public User? Owner { get; set; }
    public Department? Department { get; set; }
    public InternalAudit? InternalAudit { get; set; }
    public ICollection<ClosureRequest> ClosureRequests { get; set; } = [];
    public ICollection<AuditFindingAction> Actions { get; set; } = [];
    public ICollection<FindingAttachment> Attachments { get; set; } = [];
}

public class AuditFindingAction
{
    public int Id { get; set; }
    public int FindingId { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [MaxLength(200)]
    public string? Responsible { get; set; }

    public DateOnly? DueDate { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "planned"; // planned|in_progress|completed|cancelled

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

public class FindingAttachment
{
    public int Id { get; set; }
    public int FindingId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int UploadedById { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User UploadedBy { get; set; } = null!;
}

public class ClosureRequest
{
    public int Id { get; set; }
    public int FindingId { get; set; }

    [Required, MaxLength(2000)]
    public string Description { get; set; } = "";

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    public int RequestedById { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending|approved|rejected

    public int? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }

    [MaxLength(1000)]
    public string? ReviewNotes { get; set; }

    public AuditFinding Finding { get; set; } = null!;
    public User RequestedBy { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
