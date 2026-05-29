using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

file static class AuditSeed
{
    public static (AppDbContext db, AuditService svc) Build()
    {
        var db  = TestDb.Create();
        var svc = new AuditService(db);
        return (db, svc);
    }

    public static User AddUser(AppDbContext db, string role)
    {
        var u = new User { Username = $"{role}_{Guid.NewGuid():N}".Substring(0,20), FullName = role, Role = role, Department = role };
        db.Users.Add(u); db.SaveChanges(); return u;
    }

    public static InternalAudit AddAudit(AppDbContext db, int leadId)
    {
        var a = new InternalAudit { Code = $"ID-T-{Guid.NewGuid():N}".Substring(0,12), Title = "Test Denetim", Period = "2026", LeadAuditorId = leadId };
        db.InternalAudits.Add(a); db.SaveChanges(); return a;
    }

    public static AuditFinding AddFinding(AppDbContext db, int auditorId, int? auditId = null)
    {
        var f = new AuditFinding { Code = $"B-T-{Guid.NewGuid():N}".Substring(0,10), Title = "Test Bulgu", AuditorId = auditorId, InternalAuditId = auditId };
        db.AuditFindings.Add(f); db.SaveChanges(); return f;
    }
}

// ── Denetim oluşturma ────────────────────────────────────────────────────────

public class AuditCreationTests
{
    [Fact]
    public void CreateAudit_GeneratesUniqueCode()
    {
        var (db, svc) = AuditSeed.Build();
        var lead = AuditSeed.AddUser(db, "auditor");

        var a1 = svc.CreateAudit("Denetim 1", "ordinary", null, null, "2026", null, null, lead.Id);
        var a2 = svc.CreateAudit("Denetim 2", "ordinary", null, null, "2026", null, null, lead.Id);

        Assert.NotEqual(a1.Code, a2.Code);
        Assert.StartsWith("ID-", a1.Code);
    }

    [Fact]
    public void CreateAudit_StartsAsPlanned()
    {
        var (db, svc) = AuditSeed.Build();
        var lead = AuditSeed.AddUser(db, "auditor");

        var audit = svc.CreateAudit("Test", "ordinary", null, null, "2026", null, null, lead.Id);

        Assert.Equal("planned", audit.Status);
    }

    [Fact]
    public void GenerateAuditCode_AfterDeletion_NoConflictWithExisting()
    {
        var (db, svc) = AuditSeed.Build();
        var lead = AuditSeed.AddUser(db, "auditor");

        var a1 = svc.CreateAudit("A1", null, null, null, "2026", null, null, lead.Id);
        var a2 = svc.CreateAudit("A2", null, null, null, "2026", null, null, lead.Id);
        db.InternalAudits.Remove(a2); db.SaveChanges();
        var a3 = svc.CreateAudit("A3", null, null, null, "2026", null, null, lead.Id);

        // a1 hâlâ var — a3 ile çakışmamalı
        Assert.NotEqual(a1.Code, a3.Code);
        // a3 geçerli bir kod formatına sahip olmalı
        Assert.StartsWith("ID-", a3.Code);
    }
}

// ── Bulgu yaşam döngüsü ──────────────────────────────────────────────────────

public class FindingLifecycleTests
{
    [Fact]
    public void CreateFinding_GeneratesUniqueCode_StartsOpen()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");

        var f1 = svc.CreateFinding("Bulgu 1", null, "Mali", "Yüksek", null, auditor.Id, null, null, null);
        var f2 = svc.CreateFinding("Bulgu 2", null, "Mali", "Orta",   null, auditor.Id, null, null, null);

        Assert.NotEqual(f1.Code, f2.Code);
        Assert.Equal("open",  f1.Status);
        Assert.Equal("open",  f2.Status);
    }

    [Fact]
    public void UpdateFinding_UpdatesFields()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = svc.CreateFinding("Eski başlık", null, null, null, null, auditor.Id, null, null, null);

        var ok = svc.InternalUpdateFinding(f.Id, "Yeni başlık", "Açıklama", "Operasyonel", "Kritik", null, null, null);

        Assert.True(ok);
        var updated = db.AuditFindings.Find(f.Id)!;
        Assert.Equal("Yeni başlık", updated.Title);
        Assert.Equal("Kritik", updated.Severity);
    }

    [Fact]
    public void UpdateFinding_ClosedFinding_ReturnsFalse()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = svc.CreateFinding("Test", null, null, null, null, auditor.Id, null, null, null);
        f.Status = "closed"; db.SaveChanges();

        var ok = svc.InternalUpdateFinding(f.Id, "Değişiklik", null, null, null, null, null, null);

        Assert.False(ok);
    }

    [Fact]
    public void AddFindingAction_SetsActionDecision()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = AuditSeed.AddFinding(db, auditor.Id);

        svc.InternalAddFindingAction(f.Id, "Kontrol güçlendir", "BT", null, auditor.Id);

        var updated = db.AuditFindings.Find(f.Id)!;
        Assert.Equal("action_planned", updated.ActionDecision);
    }

    [Fact]
    public void UpdateFindingActionStatus_Completed_SetsCompletedAt()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var action = svc.InternalAddFindingAction(f.Id, "Aksiyon", null, null, auditor.Id);

        svc.InternalUpdateFindingActionStatus(f.Id, action.Id, "completed");

        var updated = db.AuditFindingActions.Find(action.Id)!;
        Assert.Equal("completed", updated.Status);
        Assert.NotNull(updated.CompletedAt);
    }
}

// ── Kapanış başvurusu akışı ──────────────────────────────────────────────────

public class ClosureRequestTests
{
    [Fact]
    public void SubmitClosureRequest_SetsStatusToClosureRequested()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var owner   = AuditSeed.AddUser(db, "finding_owner");
        var f = AuditSeed.AddFinding(db, auditor.Id);

        svc.InternalSubmitClosureRequest(f.Id, "Kontroller uygulandı", null, owner.Id);

        Assert.Equal("closure_requested", db.AuditFindings.Find(f.Id)!.Status);
    }

    [Fact]
    public void ReviewClosureRequest_Approved_ClosesFinding()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var req = svc.InternalSubmitClosureRequest(f.Id, "Kanıt eklendi", null, auditor.Id);

        svc.InternalReviewClosureRequest(f.Id, req.Id, "approved", "Uygun", mgr.Id);

        var finding = db.AuditFindings.Find(f.Id)!;
        Assert.Equal("closed", finding.Status);
        Assert.NotNull(finding.ClosedAt);
    }

    [Fact]
    public void ReviewClosureRequest_Rejected_ReopensAndLeavesOpen()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var req = svc.InternalSubmitClosureRequest(f.Id, "İlk başvuru", null, auditor.Id);

        svc.InternalReviewClosureRequest(f.Id, req.Id, "rejected", "Yetersiz kanıt", mgr.Id);

        Assert.Equal("open", db.AuditFindings.Find(f.Id)!.Status);
    }

    [Fact]
    public void ReviewClosureRequest_RecordsReviewerAndTimestamp()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var req = svc.InternalSubmitClosureRequest(f.Id, "Test", null, auditor.Id);

        var before = DateTime.UtcNow;
        svc.InternalReviewClosureRequest(f.Id, req.Id, "approved", "Ok", mgr.Id);

        var updated = db.ClosureRequests.Find(req.Id)!;
        Assert.Equal("approved", updated.Status);
        Assert.Equal(mgr.Id, updated.ReviewedById);
        Assert.True(updated.ReviewedAt >= before);
    }

    [Fact]
    public void MultipleClosureAttempts_AllRecorded()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);

        var req1 = svc.InternalSubmitClosureRequest(f.Id, "İlk deneme", null, auditor.Id);
        svc.InternalReviewClosureRequest(f.Id, req1.Id, "rejected", "Eksik", mgr.Id);
        var req2 = svc.InternalSubmitClosureRequest(f.Id, "İkinci deneme", null, auditor.Id);
        svc.InternalReviewClosureRequest(f.Id, req2.Id, "approved", "Tamam", mgr.Id);

        Assert.Equal(2, db.ClosureRequests.Count(r => r.FindingId == f.Id));
        Assert.Equal("closed", db.AuditFindings.Find(f.Id)!.Status);
    }
}
