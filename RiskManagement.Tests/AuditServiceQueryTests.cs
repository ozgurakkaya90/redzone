using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

file static class AQSeed
{
    public static (AppDbContext db, AuditService svc) Build()
    {
        var db  = TestDb.Create();
        return (db, new AuditService(db));
    }

    public static User AddUser(AppDbContext db, string role)
    {
        var u = new User { Username = $"{role}_{Guid.NewGuid():N}".Substring(0,20), FullName = role, Role = role };
        db.Users.Add(u); db.SaveChanges(); return u;
    }

    public static InternalAudit AddAudit(AppDbContext db, int leadId, string status = "planned")
    {
        var a = new InternalAudit { Code = $"ID-{Guid.NewGuid():N}".Substring(0,12), Title = "Test", Period = "2026", LeadAuditorId = leadId, Status = status };
        db.InternalAudits.Add(a); db.SaveChanges(); return a;
    }

    public static AuditFinding AddFinding(AppDbContext db, int auditorId, int? auditId = null,
        string? category = null, string? severity = null, string status = "open", int? ownerId = null)
    {
        var f = new AuditFinding { Code = $"B-{Guid.NewGuid():N}".Substring(0,10), Title = "Bulgu",
            AuditorId = auditorId, InternalAuditId = auditId, Category = category,
            Severity = severity, Status = status, OwnerId = ownerId };
        db.AuditFindings.Add(f); db.SaveChanges(); return f;
    }
}

// ── GetAudits / GetAuditsForUser ─────────────────────────────────────────────

public class AuditQueryTests
{
    [Fact]
    public void GetAudits_StatusFilter_ReturnsOnlyMatching()
    {
        var (db, svc) = AQSeed.Build();
        var lead = AQSeed.AddUser(db, "auditor");
        AQSeed.AddAudit(db, lead.Id, "planned");
        AQSeed.AddAudit(db, lead.Id, "in_progress");
        AQSeed.AddAudit(db, lead.Id, "in_progress");

        Assert.Single(svc.GetAudits("planned"));
        Assert.Equal(2, svc.GetAudits("in_progress").Count);
    }

    [Fact]
    public void GetAuditsForUser_AuditManager_SeesAll()
    {
        var (db, svc) = AQSeed.Build();
        var mgr  = AQSeed.AddUser(db, "audit_manager");
        var lead = AQSeed.AddUser(db, "auditor");
        AQSeed.AddAudit(db, lead.Id);
        AQSeed.AddAudit(db, lead.Id);

        Assert.Equal(2, svc.GetAuditsForUser(mgr.Id, "audit_manager").Count);
    }

    [Fact]
    public void GetAuditsForUser_Auditor_SeesOnlyAssigned()
    {
        var (db, svc) = AQSeed.Build();
        var a1 = AQSeed.AddUser(db, "auditor");
        var a2 = AQSeed.AddUser(db, "auditor");
        AQSeed.AddAudit(db, a1.Id);
        AQSeed.AddAudit(db, a2.Id);

        Assert.Single(svc.GetAuditsForUser(a1.Id, "auditor"));
    }

    [Fact]
    public void GetAuditsForUser_UnrelatedRole_SeesNothing()
    {
        var (db, svc) = AQSeed.Build();
        var lead  = AQSeed.AddUser(db, "auditor");
        var other = AQSeed.AddUser(db, "user");
        AQSeed.AddAudit(db, lead.Id);

        Assert.Empty(svc.GetAuditsForUser(other.Id, "user"));
    }

    [Fact]
    public void GetAudit_ExistingId_ReturnsAudit()
    {
        var (db, svc) = AQSeed.Build();
        var lead  = AQSeed.AddUser(db, "auditor");
        var audit = AQSeed.AddAudit(db, lead.Id);

        Assert.NotNull(svc.GetAudit(audit.Id));
        Assert.Null(svc.GetAudit(99999));
    }

    [Fact]
    public async Task UpdateAudit_UpdatesFields()
    {
        var (db, svc) = AQSeed.Build();
        var lead  = AQSeed.AddUser(db, "auditor");
        var audit = AQSeed.AddAudit(db, lead.Id);

        var ok = await svc.UpdateAuditAsync(audit.Id, "Yeni Başlık", "follow_up", "BT Birimi",
            "BT sistemleri", "2026", null, null, "in_progress");

        Assert.True(ok);
        var updated = db.InternalAudits.Find(audit.Id)!;
        Assert.Equal("Yeni Başlık", updated.Title);
        Assert.Equal("in_progress", updated.Status);
    }

    [Fact]
    public async Task UpdateAudit_NonExistent_ReturnsFalse()
    {
        var (_, svc) = AQSeed.Build();
        Assert.False(await svc.UpdateAuditAsync(99999, "X", null, null, null, "2026", null, null, "planned"));
    }
}

// ── GetFindings filtreleri ───────────────────────────────────────────────────

public class FindingQueryTests
{
    [Fact]
    public void GetFindings_CategoryFilter()
    {
        var (db, svc) = AQSeed.Build();
        var aud = AQSeed.AddUser(db, "auditor");
        AQSeed.AddFinding(db, aud.Id, category: "Mali");
        AQSeed.AddFinding(db, aud.Id, category: "Operasyonel");
        AQSeed.AddFinding(db, aud.Id, category: "Mali");

        Assert.Equal(2, svc.GetFindings(category: "Mali").Count);
        Assert.Single(svc.GetFindings(category: "Operasyonel"));
    }

    [Fact]
    public void GetFindings_SeverityFilter()
    {
        var (db, svc) = AQSeed.Build();
        var aud = AQSeed.AddUser(db, "auditor");
        AQSeed.AddFinding(db, aud.Id, severity: "Kritik");
        AQSeed.AddFinding(db, aud.Id, severity: "Yüksek");

        Assert.Single(svc.GetFindings(severity: "Kritik"));
    }

    [Fact]
    public void GetFindings_StatusFilter()
    {
        var (db, svc) = AQSeed.Build();
        var aud = AQSeed.AddUser(db, "auditor");
        AQSeed.AddFinding(db, aud.Id, status: "open");
        AQSeed.AddFinding(db, aud.Id, status: "closed");

        Assert.Single(svc.GetFindings(status: "open"));
        Assert.Single(svc.GetFindings(status: "closed"));
    }

    [Fact]
    public void GetFindings_AuditIdFilter()
    {
        var (db, svc) = AQSeed.Build();
        var aud   = AQSeed.AddUser(db, "auditor");
        var audit = AQSeed.AddAudit(db, aud.Id);
        AQSeed.AddFinding(db, aud.Id, auditId: audit.Id);
        AQSeed.AddFinding(db, aud.Id, auditId: audit.Id);
        AQSeed.AddFinding(db, aud.Id); // farklı denetim

        Assert.Equal(2, svc.GetFindings(auditId: audit.Id).Count);
    }

    [Fact]
    public void GetFinding_ExistingId_ReturnsFinding()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var finding = AQSeed.AddFinding(db, aud.Id);

        Assert.NotNull(svc.GetFinding(finding.Id));
        Assert.Null(svc.GetFinding(99999));
    }

    [Fact]
    public void GetFindingForUser_AdminManager_AlwaysReturns()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var mgr     = AQSeed.AddUser(db, "audit_manager");
        var finding = AQSeed.AddFinding(db, aud.Id);

        Assert.NotNull(svc.GetFindingForUser(finding.Id, mgr.Id, "audit_manager"));
    }

    [Fact]
    public void GetFindingForUser_UnrelatedUser_ReturnsNull()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var other   = AQSeed.AddUser(db, "user");
        var finding = AQSeed.AddFinding(db, aud.Id);

        Assert.Null(svc.GetFindingForUser(finding.Id, other.Id, "user"));
    }

    [Fact]
    public void CanAccessFinding_Owner_ReturnsTrue()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var owner   = AQSeed.AddUser(db, "finding_owner");
        var finding = AQSeed.AddFinding(db, aud.Id, ownerId: owner.Id);

        Assert.True(svc.CanAccessFinding(finding, owner.Id, "finding_owner"));
    }

    [Fact]
    public async Task SetActionDecision_UpdatesField()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var finding = AQSeed.AddFinding(db, aud.Id);

        await svc.InternalSetActionDecisionAsync(finding.Id, "risk_accepted");

        Assert.Equal("risk_accepted", db.AuditFindings.Find(finding.Id)!.ActionDecision);
    }

    [Fact]
    public async Task DeleteFindingAction_RemovesAction()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var finding = AQSeed.AddFinding(db, aud.Id);
        var action  = await svc.InternalAddFindingActionAsync(finding.Id, "Aksiyon", null, null, aud.Id);

        Assert.True(await svc.InternalDeleteFindingActionAsync(finding.Id, action.Id));
        Assert.Null(db.AuditFindingActions.Find(action.Id));
    }

    [Fact]
    public async Task DeleteFindingAction_NonExistent_ReturnsFalse()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var finding = AQSeed.AddFinding(db, aud.Id);

        Assert.False(await svc.InternalDeleteFindingActionAsync(finding.Id, 99999));
    }
}

// ── Dashboard stats ──────────────────────────────────────────────────────────

public class AuditDashboardTests
{
    [Fact]
    public void GetDashboard_CountsCorrectly()
    {
        var (db, svc) = AQSeed.Build();
        var aud = AQSeed.AddUser(db, "auditor");
        var severities = new[] { "Kritik", "Yüksek", "Orta", "Düşük" };

        AQSeed.AddFinding(db, aud.Id, status: "open",    severity: "Kritik");
        AQSeed.AddFinding(db, aud.Id, status: "open",    severity: "Yüksek");
        AQSeed.AddFinding(db, aud.Id, status: "closed",  severity: "Orta");
        AQSeed.AddFinding(db, aud.Id, status: "closure_requested", severity: "Düşük");

        var stats = svc.GetDashboard(severities);

        Assert.Equal(4, stats.Total);
        Assert.Equal(2, stats.Open);
        Assert.Equal(1, stats.Closed);
        Assert.Equal(1, stats.ClosureRequested);
    }

    [Fact]
    public void GetDashboard_OverdueFindings_CountedCorrectly()
    {
        var (db, svc) = AQSeed.Build();
        var aud = AQSeed.AddUser(db, "auditor");
        var severities = new[] { "Kritik", "Yüksek", "Orta", "Düşük" };

        var overdue = AQSeed.AddFinding(db, aud.Id, status: "open");
        overdue.DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10));
        var fresh = AQSeed.AddFinding(db, aud.Id, status: "open");
        fresh.DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
        db.SaveChanges();

        var stats = svc.GetDashboard(severities);

        Assert.Equal(1, stats.Overdue);
    }

    [Fact]
    public void GetDashboard_BySeverity_GroupsCorrectly()
    {
        var (db, svc) = AQSeed.Build();
        var aud = AQSeed.AddUser(db, "auditor");
        var severities = new[] { "Kritik", "Yüksek", "Orta", "Düşük" };

        AQSeed.AddFinding(db, aud.Id, severity: "Kritik");
        AQSeed.AddFinding(db, aud.Id, severity: "Kritik");
        AQSeed.AddFinding(db, aud.Id, severity: "Yüksek");

        var stats = svc.GetDashboard(severities);

        Assert.Equal(2, stats.BySeverity["Kritik"]);
        Assert.Equal(1, stats.BySeverity["Yüksek"]);
        Assert.Equal(0, stats.BySeverity["Orta"]);
    }

    [Fact]
    public async Task GetAllFindingActions_ReturnsAllActions()
    {
        var (db, svc) = AQSeed.Build();
        var aud     = AQSeed.AddUser(db, "auditor");
        var f1 = AQSeed.AddFinding(db, aud.Id);
        var f2 = AQSeed.AddFinding(db, aud.Id);
        await svc.InternalAddFindingActionAsync(f1.Id, "Aksiyon 1", null, null, aud.Id);
        await svc.InternalAddFindingActionAsync(f2.Id, "Aksiyon 2", null, null, aud.Id);

        Assert.Equal(2, svc.GetAllFindingActions().Count);
    }
}
