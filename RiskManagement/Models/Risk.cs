namespace RiskManagement.Models;

public class Risk
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ResponsibleUnit { get; set; }
    public string? RiskStrategy { get; set; }
    public int? ProposedById { get; set; }
    public string? ProposerName { get; set; }
    public DateTime ProposedAt { get; set; } = DateTime.UtcNow;
    public int? OwnerId { get; set; }
    // proposed→under_review→approved→strategy_set→controlled→residual_evaluated→action_planned | rejected
    public string Status { get; set; } = "proposed";
    public string? RejectionReason { get; set; }

    public User? ProposedBy { get; set; }
    public User? Owner { get; set; }
    public ICollection<Evaluation> Evaluations { get; set; } = [];
    public ICollection<Control> Controls { get; set; } = [];
    public ICollection<ActionPlan> ActionPlans { get; set; } = [];
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
    public string Description { get; set; } = "";
    public string ControlType { get; set; } = "Önleyici";
    public string? Effectiveness { get; set; }
    public string? Frequency { get; set; }
    public int EnteredById { get; set; }
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;

    public Risk Risk { get; set; } = null!;
    public User EnteredBy { get; set; } = null!;
}

public class ActionPlan
{
    public int Id { get; set; }
    public int RiskId { get; set; }
    public string Description { get; set; } = "";
    public string Responsible { get; set; } = "";
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = "planned"; // planned|in_progress|completed|cancelled
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Risk Risk { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
}
