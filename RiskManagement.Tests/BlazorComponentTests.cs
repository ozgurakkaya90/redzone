using Bunit;
using Microsoft.Extensions.Caching.Memory;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

// ─── RiskWorkflow durum geçiş kuralları ─────────────────────────────────────
// Bu testler iş akışı mantığını saf olarak doğrular;
// Blazor component render'ı gerektirmeyen saf C# testleridir.

public class RiskWorkflowStateMachineTests
{
    [Theory]
    [InlineData("proposed",          "under_review",      true)]
    [InlineData("proposed",          "rejected",          true)]
    [InlineData("proposed",          "approved",          false)]  // adım atlama yasak
    [InlineData("under_review",      "awaiting_approval", true)]
    [InlineData("under_review",      "approved",          true)]
    [InlineData("under_review",      "rejected",          true)]
    [InlineData("awaiting_approval", "approved",          true)]
    [InlineData("awaiting_approval", "rejected",          true)]
    [InlineData("awaiting_approval", "under_review",      true)]
    [InlineData("approved",          "strategy_set",      true)]
    [InlineData("approved",          "proposed",          false)]
    [InlineData("strategy_set",      "controlled",        true)]
    [InlineData("controlled",        "residual_evaluated",true)]
    [InlineData("residual_evaluated","action_planned",    true)]
    [InlineData("residual_evaluated","risk_accepted",     true)]
    [InlineData("action_planned",    "controlled",        true)]
    [InlineData("rejected",          "under_review",      true)]
    [InlineData("rejected",          "approved",          false)]
    [InlineData("risk_accepted",     "proposed",          false)] // terminal
    [InlineData("closed",            "proposed",          false)] // terminal
    public void CanTransition_MatchesWorkflowRules(string from, string to, bool expected)
    {
        Assert.Equal(expected, RiskWorkflow.CanTransition(from, to));
    }

    [Theory]
    [InlineData("risk_accepted", true)]
    [InlineData("closed",        true)]
    [InlineData("proposed",      false)]
    [InlineData("approved",      false)]
    [InlineData("rejected",      false)]
    public void IsTerminal_CorrectlyIdentifiesTerminalStates(string status, bool expected)
    {
        Assert.Equal(expected, RiskWorkflow.IsTerminal(status));
    }

    [Fact]
    public void EnsureCanTransition_InvalidTransition_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RiskWorkflow.EnsureCanTransition("proposed", "closed"));
    }

    [Fact]
    public void EnsureCanTransition_ValidTransition_DoesNotThrow()
    {
        var ex = Record.Exception(() =>
            RiskWorkflow.EnsureCanTransition("proposed", "under_review"));
        Assert.Null(ex);
    }

    [Fact]
    public void AllowedTransitions_Proposed_ContainsExpectedStates()
    {
        var allowed = RiskWorkflow.AllowedTransitions("proposed");
        Assert.Contains("under_review", allowed);
        Assert.Contains("rejected", allowed);
        Assert.DoesNotContain("approved", allowed);
    }
}

// ─── İzin Matrisi Doğrulama ──────────────────────────────────────────────────

public class PermissionMatrixTests
{
    private static AuthService BuildAuth()
    {
        var db = TestDb.Create();
        return new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
    }

    private static User UserWithRole(AppDbContext db, string role)
    {
        var u = new User { Username = $"{role}_{Guid.NewGuid():N}".Substring(0,20), FullName = role, Role = role };
        db.Users.Add(u);
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleName = role });
        db.SaveChanges();
        return u;
    }

    [Theory]
    [InlineData("admin",        "risk.read",    true)]
    [InlineData("admin",        "risk.approve", true)]
    [InlineData("admin",        "ethics.board_review", true)]
    [InlineData("committee",    "risk.approve", true)]
    [InlineData("committee",    "risk.write",   false)]  // komite risk oluşturamaz
    [InlineData("risk_manager", "risk.manage",  true)]
    [InlineData("risk_manager", "risk.approve", false)]  // onay sadece komitede
    [InlineData("user",         "risk.read",    true)]
    [InlineData("user",         "risk.approve", false)]
    [InlineData("user",         "audit.modify", false)]
    [InlineData("auditor",      "audit.read",   true)]
    [InlineData("auditor",      "audit.write",  true)]
    [InlineData("auditor",      "risk.approve", false)]
    [InlineData("finding_owner","audit.modify", true)]
    [InlineData("finding_owner","audit.close_approve", false)]
    [InlineData("ethics_board", "ethics.board_review", true)]
    [InlineData("ethics_board", "audit.close_approve", false)]
    public void DefaultPermissions_MatchDesignSpec(string role, string permission, bool expected)
    {
        var hasDefault = AuthService.DefaultPermissions.TryGetValue(role, out var perms)
                         && perms.Contains(permission);
        Assert.Equal(expected, hasDefault);
    }

    [Fact]
    public void AllRoles_HaveEthicsWrite_ForAnonymousTipLine()
    {
        foreach (var role in AuthService.AllRoles)
        {
            if (!AuthService.DefaultPermissions.TryGetValue(role, out var perms)) continue;
            Assert.True(perms.Contains("ethics.write"),
                $"Rol '{role}' ethics.write iznine sahip olmalı (herkes etik bildirim yapabilir)");
        }
    }

    [Fact]
    public void Admin_HasAllPermissions()
    {
        var adminPerms = AuthService.DefaultPermissions["admin"];
        foreach (var perm in AuthService.Permissions.Keys)
            Assert.Contains(perm, adminPerms);
    }

    [Fact]
    public void HasPermission_CustomRole_UsesRolePermissionsTable()
    {
        var db   = TestDb.Create();
        var auth = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));

        var customRole = new CustomRole { Name = "custom_reviewer", Label = "Özel İzleyici" };
        db.CustomRoles.Add(customRole);
        db.RolePermissions.Add(new RolePermission { Role = "custom_reviewer", Permission = "risk.read" });
        db.SaveChanges();

        var user = new User { Username = "customu", FullName = "Custom", Role = "custom_reviewer" };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = "custom_reviewer" });
        db.SaveChanges();

        Assert.True(auth.HasPermission(user, "risk.read"));
        Assert.False(auth.HasPermission(user, "risk.approve"));
    }
}

// ─── Risk Durum Geçişi Yetkilendirme Testleri ────────────────────────────────

public class RiskStatusAuthorizationTests
{
    private static (AppDbContext db, RiskService svc) Build()
    {
        var db   = TestDb.Create();
        var cfgSvc  = TestDb.CreateConfigService(db);
        var calc = new RiskCalculator(cfgSvc);
        var auth = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var svc  = new RiskService(db, calc, auth);
        return (db, svc);
    }

    [Fact]
    public async Task OnlyCommittee_CanApprove_AwaitingApproval()
    {
        var (db, svc) = Build();
        var committee   = new User { Username = "com", FullName = "Committee", Role = "committee" };
        var risk_manager = new User { Username = "rmgr", FullName = "RMgr", Role = "risk_manager" };
        db.Users.AddRange(committee, risk_manager);
        db.SaveChanges();
        db.UserRoles.AddRange(
            new UserRole { UserId = committee.Id, RoleName = "committee" },
            new UserRole { UserId = risk_manager.Id, RoleName = "risk_manager" });
        db.SaveChanges();

        var risk = new Risk { Code = "R-AUTH-001", Title = "Auth Test", Status = "awaiting_approval" };
        db.Risks.Add(risk); db.SaveChanges();

        Assert.True(await svc.UpdateStatusAsync(risk.Id, "approved", null, committee));
        Assert.False(await svc.UpdateStatusAsync(risk.Id, "approved", null, risk_manager));
    }

    [Fact]
    public async Task AcceptRisk_RequiresCorrectWorkflowState()
    {
        var (db, svc) = Build();
        var mgr = new User { Username = "mgr2", FullName = "Mgr", Role = "risk_manager" };
        db.Users.Add(mgr); db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = mgr.Id, RoleName = "risk_manager" });
        db.SaveChanges();

        // risk_accepted'a geçiş için residual_evaluated olmalı
        var risk = new Risk { Code = "R-AUTH-002", Title = "Accept Test", Status = "approved" };
        db.Risks.Add(risk); db.SaveChanges();

        var (ok, err) = await svc.AcceptRiskAsync(risk.Id, "Kabul gerekçesi", mgr);
        Assert.False(ok);
        Assert.NotNull(err);

        // Doğru state'e taşı
        risk.Status = "residual_evaluated";
        db.SaveChanges();

        (ok, err) = await svc.AcceptRiskAsync(risk.Id, "Kabul gerekçesi", mgr);
        Assert.True(ok);
        Assert.Equal("risk_accepted", db.Risks.Find(risk.Id)!.Status);

        // Kabul gerekçesi hem kayda hem denetim izine doğru alanla yazılmalı (regresyon: log
        // önceden RejectionReason'ı geçiriyordu → denetim izinde gerekçe boş/yanlış görünüyordu).
        Assert.Equal("Kabul gerekçesi", db.Risks.Find(risk.Id)!.AcceptanceReason);
        var acceptLog = db.RiskAuditLogs
            .Where(l => l.RiskId == risk.Id && l.Action == "Kalıntı Risk Kabul Edildi")
            .OrderByDescending(l => l.Id)
            .First();
        Assert.Equal("Kabul gerekçesi", acceptLog.NewValue);
    }

    [Fact]
    public async Task AcceptRisk_DeniedFor_PlainRiskOwner_WithoutManagePermission()
    {
        var (db, svc) = Build();
        // Salt risk sahibi: risk.manage yetkisi YOK (risk_owner varsayılan izinlerinde yok).
        var owner = new User { Username = "owner1", FullName = "Owner", Role = "risk_owner" };
        db.Users.Add(owner); db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = owner.Id, RoleName = "risk_owner" });
        db.SaveChanges();

        // Geçiş durumu uygun (residual_evaluated) ve kullanıcı bu riskin sahibi.
        var risk = new Risk { Code = "R-AUTH-003", Title = "Owner Bypass Test",
            Status = "residual_evaluated", OwnerId = owner.Id };
        db.Risks.Add(risk); db.SaveChanges();

        // UI kestirmesi (IsThisRiskOwner) butonu gösterse de servis yetki kapısı reddetmeli.
        var (ok, err) = await svc.AcceptRiskAsync(risk.Id, "Sahip kabulü", owner);
        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Equal("residual_evaluated", db.Risks.Find(risk.Id)!.Status); // durum değişmedi
    }
}
