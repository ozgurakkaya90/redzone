using Microsoft.Extensions.Caching.Memory;
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
        var cfg  = TestDb.CreateConfigService(db);
        var calc = new RiskCalculator(cfg);
        var auth = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var svc  = new RiskService(db, calc, auth);
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
    public async Task RiskManager_CanTake_Proposed_ToUnderReview()
    {
        var (db, _, svc) = Seed.Build();
        var mgr   = Seed.MakeUser(db, "risk_manager");
        var owner = Seed.MakeUser(db, "risk_owner");
        var risk  = Seed.MakeRisk(db, owner.Id);

        var ok = await svc.UpdateStatusAsync(risk.Id, "under_review", null, mgr);

        Assert.True(ok);
        Assert.Equal("under_review", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task RiskManager_CanSend_UnderReview_ToAwaitingApproval()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        var ok = await svc.UpdateStatusAsync(risk.Id, "awaiting_approval", null, mgr);

        Assert.True(ok);
        Assert.Equal("awaiting_approval", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task Committee_CanApprove_AwaitingApproval()
    {
        var (db, _, svc) = Seed.Build();
        var committee = Seed.MakeUser(db, "committee");
        var risk      = Seed.MakeRisk(db, committee.Id, "awaiting_approval");

        var ok = await svc.UpdateStatusAsync(risk.Id, "approved", null, committee);

        Assert.True(ok);
        Assert.Equal("approved", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task RegularUser_CannotApprove_Risk()
    {
        var (db, _, svc) = Seed.Build();
        var user = Seed.MakeUser(db, "user");
        var risk = Seed.MakeRisk(db, user.Id, "awaiting_approval");

        var ok = await svc.UpdateStatusAsync(risk.Id, "approved", null, user);

        Assert.False(ok);
        Assert.Equal("awaiting_approval", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task RegularUser_CannotTake_Risk_UnderReview()
    {
        var (db, _, svc) = Seed.Build();
        var user = Seed.MakeUser(db, "user");
        var risk = Seed.MakeRisk(db, user.Id, "proposed");

        var ok = await svc.UpdateStatusAsync(risk.Id, "under_review", null, user);

        Assert.False(ok);
        Assert.Equal("proposed", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task RiskManager_CanReject_Proposed()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "proposed");

        var ok = await svc.UpdateStatusAsync(risk.Id, "rejected", "Yetersiz bilgi", mgr);

        Assert.True(ok);
        var updated = db.Risks.Find(risk.Id)!;
        Assert.Equal("rejected", updated.Status);
        Assert.Equal("Yetersiz bilgi", updated.RejectionReason);
    }

    [Fact]
    public async Task RiskManager_CannotSkip_Proposed_DirectTo_Approved()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "proposed");

        var ok = await svc.UpdateStatusAsync(risk.Id, "approved", null, mgr);

        Assert.False(ok);
    }

    [Fact]
    public async Task Admin_CanDoAnyTransition()
    {
        var (db, _, svc) = Seed.Build();
        var admin = Seed.MakeUser(db, "admin");
        var risk  = Seed.MakeRisk(db, admin.Id, "proposed");

        Assert.True(await svc.UpdateStatusAsync(risk.Id, "under_review",      null, admin));
        Assert.True(await svc.UpdateStatusAsync(risk.Id, "awaiting_approval", null, admin));
        Assert.True(await svc.UpdateStatusAsync(risk.Id, "approved",          null, admin));
        Assert.True(await svc.UpdateStatusAsync(risk.Id, "strategy_set",      null, admin));
    }

    [Fact]
    public async Task Rejected_Risk_CanReturn_ToUnderReview()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "rejected");

        var ok = await svc.UpdateStatusAsync(risk.Id, "under_review", null, mgr);

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
    public async Task Transition_MatchesExpected(string from, string to, string role, bool expected)
    {
        var (db, _, svc) = Seed.Build();
        var actor = Seed.MakeUser(db, role);
        var risk  = Seed.MakeRisk(db, actor.Id, from);

        var result = await svc.UpdateStatusAsync(risk.Id, to, null, actor);

        Assert.Equal(expected, result);
    }
}

// ─── Değerlendirme testleri ──────────────────────────────────────────────────

public class RiskEvaluationTests
{
    [Fact]
    public async Task LowScore_Evaluation_AutoApproves_Risk()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        // Skor = 0.1 * 0.5 * 1 = 0.05 → < 70 → auto approved
        await svc.AddEvaluationAsync(risk.Id, "initial", 0.1, 0.5, 1.0, null, mgr.Id);

        Assert.Equal("approved", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task HighScore_Evaluation_KeepsUnderReview()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        // Skor = 10 * 10 * 100 = 10000 → >= 70 → under_review
        await svc.AddEvaluationAsync(risk.Id, "initial", 10.0, 10.0, 100.0, null, mgr.Id);

        Assert.Equal("under_review", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task ResidualEvaluation_AdvancesTo_ResidualEvaluated()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "controlled");

        await svc.AddEvaluationAsync(risk.Id, "residual", 1.0, 1.0, 1.0, null, mgr.Id);

        Assert.Equal("residual_evaluated", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task Evaluation_Score_CalculatedCorrectly()
    {
        var (db, cfg, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "under_review");

        var eval = await svc.AddEvaluationAsync(risk.Id, "initial", 3.0, 2.0, 15.0, null, mgr.Id);

        Assert.Equal(90.0, eval.Score);  // 3 * 2 * 15
    }
}

// ─── Kontrol ve Aksiyon testleri ─────────────────────────────────────────────

public class RiskControlActionTests
{
    [Fact]
    public async Task AddControl_StrategySet_Risk_AdvancesToControlled()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "strategy_set");

        await svc.AddControlAsync(risk.Id, "Test kontrol", "Önleyici", null, null, mgr.Id);

        Assert.Equal("controlled", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task AddAction_ResidualEvaluated_AdvancesToActionPlanned()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "residual_evaluated");

        await svc.AddActionAsync(risk.Id, "Test aksiyon", "IT", null, mgr.Id);

        Assert.Equal("action_planned", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task AllActionsCompleted_ResetsTo_ResidualEvaluated()
    {
        var (db, _, svc) = Seed.Build();
        var mgr    = Seed.MakeUser(db, "risk_manager");
        var risk   = Seed.MakeRisk(db, mgr.Id, "residual_evaluated");
        var action = await svc.AddActionAsync(risk.Id, "Aksiyon", "IT", null, mgr.Id);

        await svc.UpdateActionStatusAsync(risk.Id, action.Id, "completed", mgr.Id);

        Assert.Equal("residual_evaluated", db.Risks.Find(risk.Id)!.Status);
    }

    [Fact]
    public async Task AssignOwner_RequiresElevatedRole()
    {
        var (db, _, svc) = Seed.Build();
        var user  = Seed.MakeUser(db, "user");
        var mgr   = Seed.MakeUser(db, "risk_manager");
        var risk  = Seed.MakeRisk(db, user.Id);

        Assert.False(await svc.AssignOwnerAsync(risk.Id, mgr.Id, user));
        Assert.True(await svc.AssignOwnerAsync(risk.Id, mgr.Id, mgr));
        Assert.Equal(mgr.Id, db.Risks.Find(risk.Id)!.OwnerId);
    }
}

// ─── Audit log testleri ──────────────────────────────────────────────────────

public class RiskAuditLogTests
{
    [Fact]
    public async Task StatusChange_IsLogged()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");
        var risk = Seed.MakeRisk(db, mgr.Id, "proposed");

        await svc.UpdateStatusAsync(risk.Id, "under_review", null, mgr);

        var logs = db.RiskAuditLogs.Where(l => l.RiskId == risk.Id).ToList();
        Assert.Contains(logs, l => l.Action == "Durum Değişikliği");
    }

    [Fact]
    public async Task RiskCreation_IsLogged()
    {
        var (db, _, svc) = Seed.Build();
        var mgr  = Seed.MakeUser(db, "risk_manager");

        var risk = await svc.CreateAsync("Yeni Risk", null, null, null, null, mgr.Id, mgr.FullName);

        var logs = db.RiskAuditLogs.Where(l => l.RiskId == risk.Id).ToList();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Action.Contains("Önerildi"));
    }
}
