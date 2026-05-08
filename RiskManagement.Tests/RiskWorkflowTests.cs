using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

// ─── helpers ────────────────────────────────────────────────────────────────

file static class Seed
{
    public static (AppDbContext db, ConfigService cfg, RiskService svc) Build()
    {
        var db   = TestDb.Create();
        var cfg  = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var calc = new RiskCalculator(cfg);
        var svc  = new RiskService(db, calc);
        return (db, cfg, svc);
    }

    public static User MakeUser(AppDbContext db, string role, int? id = null)
    {
        var u = new User
        {
            Id       = id ?? 0,
            Username = $"user_{role}_{Guid.NewGuid():N}".Substring(0, 20),
            FullName = role,
            Role     = role,
            Department = role,
        };
        db.Users.Add(u);
        db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleName = role });
        db.SaveChanges();
        return u;
    }

    public static Risk MakeRisk(AppDbContext db, int proposedById, string status = "proposed")
    {
        var r = new Risk
        {
            Code        = $"R-TEST-{Guid.NewGuid().ToString("N")[..8]}",
            Title       = "Test Riski",
            Status      = status,
            ProposedById = proposedById,
            ProposedAt  = DateTime.UtcNow,
        };
        db.Risks.Add(r);
        db.SaveChanges();
        return r;
    }
}

// ─── Workflow geçiş testleri ─────────────────────────────────────────────────

public class RiskWorkflowTests
{
    [Fact]
    public void RiskManager_CanTake_Proposed_ToUnderReview()
    {
        var (db, _, svc) = Seed.Build();
        var mgr   = Seed.MakeUser(db, "risk_manager");
        var owner = Seed.MakeUser(db, "risk_owner");
        var risk  = Seed.MakeRisk(db, owner.Id);

        var ok = svc.UpdateStatus(risk.Id, "under_review", null, mgr);

        Assert.True(ok);
        Assert.Equal("under_review", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void RiskManager_CanSend_UnderReview_ToAwaitingApproval()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        var ok = svc.UpdateStatus(risk.Id, "awaiting_approval", null, mgr);

        Assert.True(ok);
        Assert.Equal("awaiting_approval", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void Committee_CanApprove_AwaitingApproval()
    {
        var (db, _, svc) = Seed.Build();
        var committee = Seed.MakeUser(db, "committee");
        var risk      = Seed.MakeRisk(db, committee.Id, "awaiting_approval");

        var ok = svc.UpdateStatus(risk.Id, "approved", null, committee);

        Assert.True(ok);
        Assert.Equal("approved", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void RegularUser_CannotApprove_Risk()
    {
        var (db, _, svc) = Seed.Build();
        var user = Seed.MakeUser(db, "user");
        var risk = Seed.MakeRisk(db, user.Id, "awaiting_approval");

        var ok = svc.UpdateStatus(risk.Id, "approved", null, user);

        Assert.False(ok);
        Assert.Equal("awaiting_approval", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void RegularUser_CannotTake_Risk_UnderReview()
    {
        var (db, _, svc) = Seed.Build();
        var user = Seed.MakeUser(db, "user");
        var risk = Seed.MakeRisk(db, user.Id, "proposed");

        var ok = svc.UpdateStatus(risk.Id, "under_review", null, user);

        Assert.False(ok);
        Assert.Equal("proposed", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void RiskManager_CanReject_Proposed()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "proposed");

        var ok = svc.UpdateStatus(risk.Id, "rejected", "Yetersiz bilgi", mgr);

        Assert.True(ok);
        var updated = db.Risks.Find(risk.Id)!;
        Assert.Equal("rejected", updated.Status);
        Assert.Equal("Yetersiz bilgi", updated.RejectionReason);
    }

    [Fact]
    public void RiskManager_CannotSkip_Proposed_DirectTo_Approved()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "proposed");

        var ok = svc.UpdateStatus(risk.Id, "approved", null, mgr);

        Assert.False(ok);
    }

    [Fact]
    public void Admin_CanDoAnyTransition()
    {
        var (db, _, svc) = Seed.Build();
        var admin = Seed.MakeUser(db, "admin");
        var risk  = Seed.MakeRisk(db, admin.Id, "proposed");

        Assert.True(svc.UpdateStatus(risk.Id, "under_review",      null, admin));
        Assert.True(svc.UpdateStatus(risk.Id, "awaiting_approval", null, admin));
        Assert.True(svc.UpdateStatus(risk.Id, "approved",          null, admin));
        Assert.True(svc.UpdateStatus(risk.Id, "strategy_set",      null, admin));
    }

    [Fact]
    public void Rejected_Risk_CanReturn_ToUnderReview()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "rejected");

        var ok = svc.UpdateStatus(risk.Id, "under_review", null, mgr);

        Assert.True(ok);
        Assert.Equal("under_review", db.Risks.Find(risk.Id)!.Status);
    }
}

// ─── Geçiş matrisi — Theory ile tam kapsam ──────────────────────────────────

public class WorkflowTransitionMatrixTests
{
    // (fromStatus, toStatus, role, shouldSucceed)
    public static IEnumerable<object[]> TransitionMatrix =>
    [
        // ── Öneriden incelemeye ──────────────────────────────────────────────
        ["proposed", "under_review",      "risk_manager",  true],
        ["proposed", "under_review",      "audit_manager", true],
        ["proposed", "under_review",      "user",          false],
        ["proposed", "under_review",      "committee",     false],
        ["proposed", "rejected",          "risk_manager",  true],
        ["proposed", "approved",          "risk_manager",  false], // adım atlamak yasak
        // ── İncelemeden onaya ───────────────────────────────────────────────
        ["under_review", "awaiting_approval", "risk_manager", true],
        ["under_review", "awaiting_approval", "risk_owner",   true],
        ["under_review", "awaiting_approval", "user",         false],
        ["under_review", "rejected",          "risk_manager", true],
        // ── Komite onayı ────────────────────────────────────────────────────
        ["awaiting_approval", "approved",      "committee",     true],
        ["awaiting_approval", "approved",      "risk_manager",  false],
        ["awaiting_approval", "rejected",      "committee",     true],
        ["awaiting_approval", "under_review",  "risk_manager",  true], // geri gönder
        // ── Onaydan strateji ─────────────────────────────────────────────────
        ["approved", "strategy_set",   "admin", true],
        ["approved", "controlled",     "admin", true],
        ["approved", "proposed",       "admin", false],
        // ── İleri akış ──────────────────────────────────────────────────────
        ["strategy_set",       "controlled",        "admin", true],
        ["controlled",         "residual_evaluated", "admin", true],
        ["residual_evaluated", "action_planned",     "admin", true],
        ["residual_evaluated", "risk_accepted",      "admin", true],
        ["action_planned",     "controlled",         "admin", true],
        // ── Reddedilenden geri dönüş ─────────────────────────────────────────
        ["rejected", "under_review", "risk_manager", true],
        ["rejected", "approved",     "admin",        false],
    ];

    [Theory]
    [MemberData(nameof(TransitionMatrix))]
    public void Transition_MatchesExpected(string from, string to, string role, bool expected)
    {
        var (db, _, svc) = Seed.Build();
        var actor = Seed.MakeUser(db, role);
        var risk  = Seed.MakeRisk(db, actor.Id, from);

        var result = svc.UpdateStatus(risk.Id, to, null, actor);

        Assert.Equal(expected, result);
    }
}

// ─── Değerlendirme testleri ──────────────────────────────────────────────────

public class RiskEvaluationTests
{
    [Fact]
    public void LowScore_Evaluation_AutoApproves_Risk()
    {
        var (db, cfg, svc) = Seed.Build();
        // Konfigürasyon seed et (fk_levels için)
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        // Skor = 0.1 * 0.5 * 1 = 0.05 → < 70 → auto approved
        svc.AddEvaluation(risk.Id, "initial", 0.1, 0.5, 1.0, null, mgr.Id);

        Assert.Equal("approved", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void HighScore_Evaluation_KeepsUnderReview()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        // Skor = 10 * 10 * 100 = 10000 → >= 70 → under_review
        svc.AddEvaluation(risk.Id, "initial", 10.0, 10.0, 100.0, null, mgr.Id);

        Assert.Equal("under_review", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void ResidualEvaluation_AdvancesTo_ResidualEvaluated()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "controlled");

        svc.AddEvaluation(risk.Id, "residual", 1.0, 1.0, 1.0, null, mgr.Id);

        Assert.Equal("residual_evaluated", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void Evaluation_Score_CalculatedCorrectly()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        var eval = svc.AddEvaluation(risk.Id, "initial", 3.0, 2.0, 15.0, null, mgr.Id);

        Assert.Equal(90.0, eval.Score);  // 3 * 2 * 15
    }
}

// ─── Kontrol ve Aksiyon testleri ─────────────────────────────────────────────

public class RiskControlActionTests
{
    [Fact]
    public void AddControl_StrategySet_Risk_AdvancesToControlled()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "strategy_set");

        svc.AddControl(risk.Id, "Test kontrol", "Önleyici", null, null, mgr.Id);

        Assert.Equal("controlled", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void AddAction_ResidualEvaluated_AdvancesToActionPlanned()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "residual_evaluated");

        svc.AddAction(risk.Id, "Test aksiyon", "IT", null, mgr.Id);

        Assert.Equal("action_planned", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void AllActionsCompleted_ResetsTo_ResidualEvaluated()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "residual_evaluated");
        var action = svc.AddAction(risk.Id, "Aksiyon", "IT", null, mgr.Id);

        svc.UpdateActionStatus(risk.Id, action.Id, "completed", mgr.Id);

        Assert.Equal("residual_evaluated", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public void AssignOwner_RequiresElevatedRole()
    {
        var (db, _, svc) = Seed.Build();
        var user  = Seed.MakeUser(db, "user");
        var mgr   = Seed.MakeUser(db, "risk_manager");
        var risk  = Seed.MakeRisk(db, user.Id);

        Assert.False(svc.AssignOwner(risk.Id, mgr.Id, user));
        Assert.True(svc.AssignOwner(risk.Id, mgr.Id, mgr));
        Assert.Equal(mgr.Id, db.Risks.Find(risk.Id)!.OwnerId);
    }
}

// ─── Audit log testleri ──────────────────────────────────────────────────────

public class RiskAuditLogTests
{
    [Fact]
    public void StatusChange_IsLogged()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "proposed");

        svc.UpdateStatus(risk.Id, "under_review", null, mgr);

        var logs = db.RiskAuditLogs.Where(l => l.RiskId == risk.Id).ToList();
        Assert.Contains(logs, l => l.Action == "Durum Değişikliği");
    }

    [Fact]
    public void RiskCreation_IsLogged()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");

        var risk = svc.Create("Yeni Risk", null, null, null, null, mgr.Id, mgr.FullName);

        var logs = db.RiskAuditLogs.Where(l => l.RiskId == risk.Id).ToList();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Action.Contains("Önerildi"));
    }
}
