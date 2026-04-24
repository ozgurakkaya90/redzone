using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class RiskService(AppDbContext db)
{
    public string GenerateCode()
    {
        var year = DateTime.UtcNow.Year;
        var count = db.Risks.Count(r => r.Code.StartsWith($"R-{year}-"));
        return $"R-{year}-{(count + 1):D3}";
    }

    public IQueryable<Risk> Query() => db.Risks
        .Include(r => r.ProposedBy)
        .Include(r => r.Owner)
        .Include(r => r.Evaluations).ThenInclude(e => e.EvaluatedBy)
        .Include(r => r.Controls).ThenInclude(c => c.EnteredBy)
        .Include(r => r.ActionPlans).ThenInclude(a => a.CreatedBy);

    public Risk? GetById(int id) => Query().FirstOrDefault(r => r.Id == id);

    public List<Risk> GetAll(string? category = null, string? status = null, string? search = null)
    {
        var q = Query();
        if (!string.IsNullOrEmpty(category)) q = q.Where(r => r.Category == category);
        if (!string.IsNullOrEmpty(status))   q = q.Where(r => r.Status == status);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(r => r.Title.Contains(search) || r.Code.Contains(search));
        return [.. q.OrderByDescending(r => r.ProposedAt)];
    }

    public Risk Create(string title, string? description, string? category,
        string? responsibleUnit, string? riskStrategy,
        int? proposedById, string? proposerName)
    {
        var risk = new Risk
        {
            Code = GenerateCode(),
            Title = title,
            Description = description,
            Category = category,
            ResponsibleUnit = responsibleUnit,
            RiskStrategy = riskStrategy,
            ProposedById = proposedById,
            ProposerName = proposerName,
        };
        db.Risks.Add(risk);
        db.SaveChanges();
        return risk;
    }

    public bool UpdateStatus(int id, string newStatus, string? rejectionReason, User currentUser)
    {
        var risk = db.Risks.Find(id);
        if (risk == null) return false;

        var valid = (risk.Status, newStatus) switch
        {
            ("proposed", "under_review")          => true,
            ("under_review", "approved")           => true,
            ("approved", "strategy_set")           => true,
            ("strategy_set", "controlled")         => true,
            ("controlled", "residual_evaluated")   => true,
            ("residual_evaluated", "action_planned")=> true,
            ("proposed", "rejected")               => true,
            ("under_review", "rejected")           => true,
            _ => false
        };
        if (!valid) return false;

        risk.Status = newStatus;
        if (newStatus == "rejected") risk.RejectionReason = rejectionReason;
        db.SaveChanges();
        return true;
    }

    public bool UpdateMetadata(int id, string? responsibleUnit, string? riskStrategy)
    {
        var risk = db.Risks.Find(id);
        if (risk == null) return false;
        if (responsibleUnit != null) risk.ResponsibleUnit = string.IsNullOrEmpty(responsibleUnit) ? null : responsibleUnit;
        if (riskStrategy    != null) risk.RiskStrategy    = string.IsNullOrEmpty(riskStrategy)    ? null : riskStrategy;
        if (risk.Status == "approved" && risk.ResponsibleUnit != null && risk.RiskStrategy != null)
            risk.Status = "strategy_set";
        db.SaveChanges();
        return true;
    }

    public bool AssignOwner(int id, int ownerId)
    {
        var risk = db.Risks.Find(id);
        if (risk == null) return false;
        risk.OwnerId = ownerId;
        db.SaveChanges();
        return true;
    }

    public Evaluation AddEvaluation(int riskId, string evalType,
        double probability, double exposure, double consequence,
        string? notes, int evaluatedById)
    {
        // Remove previous evaluation of same type
        var existing = db.Evaluations
            .Where(e => e.RiskId == riskId && e.EvalType == evalType)
            .ToList();
        db.Evaluations.RemoveRange(existing);

        var score = Math.Round(probability * exposure * consequence, 2);
        var eval = new Evaluation
        {
            RiskId = riskId,
            EvalType = evalType,
            Probability = probability,
            Exposure = exposure,
            Consequence = consequence,
            Score = score,
            RiskLevel = ConfigService.CalculateRiskLevel(score),
            Notes = notes,
            EvaluatedById = evaluatedById,
        };
        db.Evaluations.Add(eval);

        // Auto-advance status
        var risk = db.Risks.Find(riskId);
        if (risk != null && evalType == "initial" && risk.Status == "under_review")
            risk.Status = "approved";

        db.SaveChanges();
        return eval;
    }

    public Control AddControl(int riskId, string description, string controlType,
        string? effectiveness, string? frequency, int enteredById)
    {
        var ctrl = new Control
        {
            RiskId = riskId,
            Description = description,
            ControlType = controlType,
            Effectiveness = effectiveness,
            Frequency = frequency,
            EnteredById = enteredById,
        };
        db.Controls.Add(ctrl);

        var risk = db.Risks.Find(riskId);
        if (risk != null && risk.Status == "strategy_set")
            risk.Status = "controlled";

        db.SaveChanges();
        return ctrl;
    }

    public bool UpdateControl(int riskId, int controlId, string description,
        string controlType, string? effectiveness, string? frequency)
    {
        var ctrl = db.Controls.FirstOrDefault(c => c.Id == controlId && c.RiskId == riskId);
        if (ctrl == null) return false;
        ctrl.Description = description;
        ctrl.ControlType = controlType;
        ctrl.Effectiveness = effectiveness;
        ctrl.Frequency = frequency;
        db.SaveChanges();
        return true;
    }

    public bool DeleteControl(int riskId, int controlId)
    {
        var ctrl = db.Controls.FirstOrDefault(c => c.Id == controlId && c.RiskId == riskId);
        if (ctrl == null) return false;
        db.Controls.Remove(ctrl);
        db.SaveChanges();
        return true;
    }

    public ActionPlan AddAction(int riskId, string description, string responsible,
        DateOnly? dueDate, int createdById)
    {
        var action = new ActionPlan
        {
            RiskId = riskId,
            Description = description,
            Responsible = responsible,
            DueDate = dueDate,
            CreatedById = createdById,
        };
        db.ActionPlans.Add(action);

        var risk = db.Risks.Find(riskId);
        if (risk != null && risk.Status == "residual_evaluated")
            risk.Status = "action_planned";

        db.SaveChanges();
        return action;
    }

    public bool UpdateActionStatus(int riskId, int actionId, string newStatus)
    {
        var action = db.ActionPlans.FirstOrDefault(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;
        action.Status = newStatus;
        if (newStatus == "completed") action.CompletedAt = DateTime.UtcNow;
        db.SaveChanges();
        return true;
    }

    public bool DeleteAction(int riskId, int actionId)
    {
        var action = db.ActionPlans.FirstOrDefault(a => a.Id == actionId && a.RiskId == riskId);
        if (action == null) return false;
        db.ActionPlans.Remove(action);
        db.SaveChanges();
        return true;
    }
}
