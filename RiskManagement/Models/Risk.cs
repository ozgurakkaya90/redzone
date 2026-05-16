using System.ComponentModel.DataAnnotations;

namespace RiskManagement.Models;

public class Risk
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

    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    [MaxLength(50)]
    public string? RiskStrategy { get; set; }

    // Kaynak & Tehlike
    [MaxLength(20)]
    public string SourceType { get; set; } = "internal"; // internal | external

    [MaxLength(200)]
    public string? Source { get; set; }

    [MaxLength(500)]
    public string? Hazard { get; set; }

    [MaxLength(500)]
    public string? PossibleImpact { get; set; }

    public string? AffectedPersons { get; set; } // JSON array

    [MaxLength(500)]
    public string? RelevantLegislation { get; set; }

    [MaxLength(500)]
    public string? CurrentStatus { get; set; }

    // Gözden geçirme
    public DateTime? LastReviewedAt { get; set; }
    public string? LastReviewerName { get; set; }
    public string? LastReviewerTitle { get; set; }

    public int? SourceLibraryItemId { get; set; }

    public int? ProposedById { get; set; }

    [MaxLength(200)]
    public string? ProposerName { get; set; }

    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    public int? OwnerId { get; set; }

    // proposed→under_review→approved→strategy_set→controlled→residual_evaluated→action_planned | rejected
    [Required, MaxLength(30)]
    public string Status { get; set; } = "proposed";

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public User? ProposedBy { get; set; }
    public User? Owner { get; set; }
    public ICollection<Evaluation> Evaluations { get; set; } = [];
    public ICollection<Control> Controls { get; set; } = [];
    public ICollection<ActionPlan> ActionPlans { get; set; } = [];
    public ICollection<RiskReview> Reviews { get; set; } = [];
    public ICollection<RiskAuditLog> AuditLogs { get; set; } = [];
}

public class RiskReview
{
    public int Id { get; set; }
    public int RiskId { get; set; }
    public DateTime MeetingDate { get; set; }
    public string? Decision { get; set; }
    public string? Notes { get; set; }
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Risk Risk { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}

public class RiskAuditLog
{
    public int Id { get; set; }
    public int RiskId { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = "";
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Risk Risk { get; set; } = null!;
    public User? User { get; set; }
}

public class Evaluation
{
    public int Id { get; set; }
    public int RiskId { get; set; }
    public string EvalType { get; set; } = "initial"; // initial | residual
    public double Probability { get; set; }
    public double Exposure { get; set; }
    public double Consequence { get; set; }
    public double Score { get; set; }
    public string RiskLevel { get; set; } = "";
    public int EvaluatedById { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }

    public Risk Risk { get; set; } = null!;
    public User EvaluatedBy { get; set; } = null!;
}

public class Control
{
    public int Id { get; set; }
    public int RiskId { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [Required, MaxLength(50)]
    public string ControlType { get; set; } = "Önleyici";

    [MaxLength(50)]
    public string? Effectiveness { get; set; }

    [MaxLength(50)]
    public string? Frequency { get; set; }

    public int EnteredById { get; set; }
    public int? OwnerDeptId { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    public Risk Risk { get; set; } = null!;
    public User EnteredBy { get; set; } = null!;
    public Department? OwnerDept { get; set; }
}

public class ActionPlan
{
    public int Id { get; set; }
    public int RiskId { get; set; }

    [Required, MaxLength(1000)]
    public string Description { get; set; } = "";

    [Required, MaxLength(200)]
    public string Responsible { get; set; } = "";

    public int? OwnerDeptId { get; set; }
    public DateOnly? DueDate { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "planned"; // planned|in_progress|completed|cancelled

    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Risk Risk { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public Department? OwnerDept { get; set; }
}
