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
        var u = new User { Username = $"{role}_{Guid.NewGuid():N}".Substring(0,20), FullName = role, Role = role };
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
    public async Task CreateAudit_GeneratesUniqueCode()
    {
        var (db, svc) = AuditSeed.Build();
        var lead = AuditSeed.AddUser(db, "auditor");

        var a1 = await svc.CreateAuditAsync("Denetim 1", "ordinary", null, null, "2026", null, null, lead.Id);
        var a2 = await svc.CreateAuditAsync("Denetim 2", "ordinary", null, null, "2026", null, null, lead.Id);

        Assert.NotEqual(a1.Code, a2.Code);
        Assert.StartsWith("ID-", a1.Code);
    }

    [Fact]
    public async Task CreateAudit_StartsAsPlanned()
    {
        var (db, svc) = AuditSeed.Build();
        var lead = AuditSeed.AddUser(db, "auditor");

        var audit = await svc.CreateAuditAsync("Test", "ordinary", null, null, "2026", null, null, lead.Id);

        Assert.Equal("planned", audit.Status);
    }

    [Fact]
    public async Task GenerateAuditCode_AfterDeletion_NoConflictWithExisting()
    {
        var (db, svc) = AuditSeed.Build();
        var lead = AuditSeed.AddUser(db, "auditor");

        var a1 = await svc.CreateAuditAsync("A1", null, null, null, "2026", null, null, lead.Id);
        var a2 = await svc.CreateAuditAsync("A2", null, null, null, "2026", null, null, lead.Id);
        db.InternalAudits.Remove(a2); db.SaveChanges();
        var a3 = await svc.CreateAuditAsync("A3", null, null, null, "2026", null, null, lead.Id);

        Assert.NotEqual(a1.Code, a3.Code);
        Assert.StartsWith("ID-", a3.Code);
    }
}

// ── Bulgu yaşam döngüsü ──────────────────────────────────────────────────────

public class FindingLifecycleTests
{
    [Fact]
    public async Task CreateFinding_GeneratesUniqueCode_StartsOpen()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");

        var f1 = await svc.CreateFindingAsync("Bulgu 1", null, "Mali", "Yüksek", null, auditor.Id, null, null, null);
        var f2 = await svc.CreateFindingAsync("Bulgu 2", null, "Mali", "Orta",   null, auditor.Id, null, null, null);

        Assert.NotEqual(f1.Code, f2.Code);
        Assert.Equal("open",  f1.Status);
        Assert.Equal("open",  f2.Status);
    }

    [Fact]
    public async Task UpdateFinding_UpdatesFields()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = await svc.CreateFindingAsync("Eski başlık", null, null, null, null, auditor.Id, null, null, null);

        var ok = await svc.InternalUpdateFindingAsync(f.Id, "Yeni başlık", "Açıklama", "Operasyonel", "Kritik", null, null, null);

        Assert.True(ok);
        var updated = db.AuditFindings.Find(f.Id)!;
        Assert.Equal("Yeni başlık", updated.Title);
        Assert.Equal("Kritik", updated.Severity);
    }

    [Fact]
    public async Task UpdateFinding_ClosedFinding_ReturnsFalse()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = await svc.CreateFindingAsync("Test", null, null, null, null, auditor.Id, null, null, null);
        f.Status = "closed"; db.SaveChanges();

        var ok = await svc.InternalUpdateFindingAsync(f.Id, "Değişiklik", null, null, null, null, null, null);

        Assert.False(ok);
    }

    [Fact]
    public async Task AddFindingAction_SetsActionDecision()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = AuditSeed.AddFinding(db, auditor.Id);

        await svc.InternalAddFindingActionAsync(f.Id, "Kontrol güçlendir", "BT", null, auditor.Id);

        var updated = db.AuditFindings.Find(f.Id)!;
        Assert.Equal("action_planned", updated.ActionDecision);
    }

    [Fact]
    public async Task UpdateFindingActionStatus_Completed_SetsCompletedAt()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var action = await svc.InternalAddFindingActionAsync(f.Id, "Aksiyon", null, null, auditor.Id);

        await svc.InternalUpdateFindingActionStatusAsync(f.Id, action.Id, "completed");

        var updated = db.AuditFindingActions.Find(action.Id)!;
        Assert.Equal("completed", updated.Status);
        Assert.NotNull(updated.CompletedAt);
    }
}

// ── Kapanış başvurusu akışı ──────────────────────────────────────────────────

public class ClosureRequestTests
{
    [Fact]
    public async Task SubmitClosureRequest_SetsStatusToClosureRequested()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var owner   = AuditSeed.AddUser(db, "finding_owner");
        var f = AuditSeed.AddFinding(db, auditor.Id);

        await svc.InternalSubmitClosureRequestAsync(f.Id, "Kontroller uygulandı", null, owner.Id);

        Assert.Equal("closure_requested", db.AuditFindings.Find(f.Id)!.Status);
    }

    [Fact]
    public async Task ReviewClosureRequest_Approved_ClosesFinding()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var req = await svc.InternalSubmitClosureRequestAsync(f.Id, "Kanıt eklendi", null, auditor.Id);

        await svc.InternalReviewClosureRequestAsync(f.Id, req!.Id, "approved", "Uygun", mgr.Id);

        var finding = db.AuditFindings.Find(f.Id)!;
        Assert.Equal("closed", finding.Status);
        Assert.NotNull(finding.ClosedAt);
    }

    [Fact]
    public async Task ReviewClosureRequest_Rejected_ReopensAndLeavesOpen()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var req = await svc.InternalSubmitClosureRequestAsync(f.Id, "İlk başvuru", null, auditor.Id);

        await svc.InternalReviewClosureRequestAsync(f.Id, req!.Id, "rejected", "Yetersiz kanıt", mgr.Id);

        Assert.Equal("open", db.AuditFindings.Find(f.Id)!.Status);
    }

    [Fact]
    public async Task ReviewClosureRequest_RecordsReviewerAndTimestamp()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);
        var req = await svc.InternalSubmitClosureRequestAsync(f.Id, "Test", null, auditor.Id);

        var before = DateTime.UtcNow;
        await svc.InternalReviewClosureRequestAsync(f.Id, req!.Id, "approved", "Ok", mgr.Id);

        var updated = db.ClosureRequests.Find(req.Id)!;
        Assert.Equal("approved", updated.Status);
        Assert.Equal(mgr.Id, updated.ReviewedById);
        Assert.True(updated.ReviewedAt >= before);
    }

    [Fact]
    public async Task MultipleClosureAttempts_AllRecorded()
    {
        var (db, svc) = AuditSeed.Build();
        var auditor = AuditSeed.AddUser(db, "auditor");
        var mgr     = AuditSeed.AddUser(db, "audit_manager");
        var f = AuditSeed.AddFinding(db, auditor.Id);

        var req1 = await svc.InternalSubmitClosureRequestAsync(f.Id, "İlk deneme", null, auditor.Id);
        await svc.InternalReviewClosureRequestAsync(f.Id, req1!.Id, "rejected", "Eksik", mgr.Id);
        var req2 = await svc.InternalSubmitClosureRequestAsync(f.Id, "İkinci deneme", null, auditor.Id);
        await svc.InternalReviewClosureRequestAsync(f.Id, req2!.Id, "approved", "Tamam", mgr.Id);

        Assert.Equal(2, db.ClosureRequests.Count(r => r.FindingId == f.Id));
        Assert.Equal("closed", db.AuditFindings.Find(f.Id)!.Status);
    }
}
