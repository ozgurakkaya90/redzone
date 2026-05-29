using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

file static class TaskSeed
{
    public static (AppDbContext db, TaskService svc) Build()
    {
        // Fabrika ile seed context'i aynı in-memory deposunu paylaşır.
        var factory = TestDb.CreateFactory();
        var db    = factory.CreateDbContext();
        var cfg   = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var cache = new MemoryCache(Options.Create(new MemoryCacheOptions { SizeLimit = 100 }));
        var svc   = new TaskService(factory, cfg, cache);
        return (db, svc);
    }

    public static User AddUser(AppDbContext db, string role, int? id = null)
    {
        var u = new User { Id = id ?? 0, Username = $"{role}_{Guid.NewGuid():N}".Substring(0, 20), FullName = role, Role = role, Department = role };
        db.Users.Add(u); db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleName = role });
        db.SaveChanges();
        return u;
    }

    public static Risk AddRisk(AppDbContext db, int userId, string status)
    {
        var r = new Risk { Code = $"R-{Guid.NewGuid():N}".Substring(0,12), Title = $"Risk {status}", Status = status, ProposedById = userId, OwnerId = userId };
        db.Risks.Add(r); db.SaveChanges(); return r;
    }
}

public class TaskServiceTests
{
    // ── Komite: proposed riskler görünür ────────────────────────────────────

    [Fact]
    public void Committee_SeesProposedRisks()
    {
        var (db, svc) = TaskSeed.Build();
        var committee = TaskSeed.AddUser(db, "committee");
        TaskSeed.AddRisk(db, committee.Id, "proposed");

        var tasks = svc.GetTasksForUser(committee.Id, "committee");

        Assert.Contains(tasks, t => t.Category == "Risk İnceleme");
    }

    // ── Komite: awaiting_approval riskler görünür ────────────────────────────

    [Fact]
    public void Committee_SeesAwaitingApprovalRisks()
    {
        var (db, svc) = TaskSeed.Build();
        var committee = TaskSeed.AddUser(db, "committee");
        TaskSeed.AddRisk(db, committee.Id, "awaiting_approval");

        var tasks = svc.GetTasksForUser(committee.Id, "committee");

        Assert.Contains(tasks, t => t.Category == "Risk Onayı");
    }

    // ── Normal kullanıcı: onay bekleyen riskleri görmez ─────────────────────

    [Fact]
    public void RegularUser_DoesNotSeeApprovalTasks()
    {
        var (db, svc) = TaskSeed.Build();
        var user      = TaskSeed.AddUser(db, "user");
        var committee = TaskSeed.AddUser(db, "committee");
        TaskSeed.AddRisk(db, committee.Id, "awaiting_approval");

        var tasks = svc.GetTasksForUser(user.Id, "user");

        Assert.DoesNotContain(tasks, t => t.Category == "Risk Onayı");
    }

    // ── Geciken aksiyon görünür ──────────────────────────────────────────────

    [Fact]
    public void OverdueAction_AppearsInTasks()
    {
        var (db, svc) = TaskSeed.Build();
        var mgr  = TaskSeed.AddUser(db, "risk_manager");
        var risk = TaskSeed.AddRisk(db, mgr.Id, "action_planned");

        db.ActionPlans.Add(new ActionPlan
        {
            RiskId      = risk.Id,
            Description = "Vadesi geçmiş aksiyon",
            Responsible = "BT",
            Status      = "planned",
            DueDate     = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
            CreatedById = mgr.Id,
        });
        db.SaveChanges();

        var tasks = svc.GetTasksForUser(mgr.Id, "risk_manager");

        Assert.Contains(tasks, t => t.Category == "Geciken Aksiyon");
    }

    // ── Tamamlanan aksiyon görev listesinde çıkar ────────────────────────────

    [Fact]
    public void CompletedAction_NotInOverdueTasks()
    {
        var (db, svc) = TaskSeed.Build();
        var mgr  = TaskSeed.AddUser(db, "risk_manager");
        var risk = TaskSeed.AddRisk(db, mgr.Id, "action_planned");

        db.ActionPlans.Add(new ActionPlan
        {
            RiskId      = risk.Id,
            Description = "Tamamlanmış",
            Responsible = "BT",
            Status      = "completed",
            DueDate     = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)),
            CreatedById = mgr.Id,
        });
        db.SaveChanges();

        var tasks = svc.GetTasksForUser(mgr.Id, "risk_manager");

        Assert.DoesNotContain(tasks, t => t.Category == "Geciken Aksiyon");
    }

    // ── Periyodik inceleme eşiği ─────────────────────────────────────────────

    [Fact]
    public void OverdueReview_AppearsinTasks_WhenThresholdExceeded()
    {
        var (db, svc) = TaskSeed.Build();
        var mgr = TaskSeed.AddUser(db, "risk_manager");
        var risk = TaskSeed.AddRisk(db, mgr.Id, "controlled");
        risk.LastReviewedAt = DateTime.UtcNow.AddDays(-100); // eşik 90 gün
        db.SaveChanges();

        var tasks = svc.GetTasksForUser(mgr.Id, "risk_manager");

        Assert.Contains(tasks, t => t.Category == "Periyodik İnceleme");
    }

    [Fact]
    public void RecentlyReviewed_Risk_NotInReviewTasks()
    {
        var (db, svc) = TaskSeed.Build();
        var mgr = TaskSeed.AddUser(db, "risk_manager");
        var risk = TaskSeed.AddRisk(db, mgr.Id, "controlled");
        risk.LastReviewedAt = DateTime.UtcNow.AddDays(-10);
        db.SaveChanges();

        var tasks = svc.GetTasksForUser(mgr.Id, "risk_manager");

        Assert.DoesNotContain(tasks, t => t.Category == "Periyodik İnceleme");
    }

    // ── Denetim kapanış onayı ────────────────────────────────────────────────

    [Fact]
    public void AuditManager_SeesPendingClosureRequests()
    {
        var (db, svc) = TaskSeed.Build();
        var mgr     = TaskSeed.AddUser(db, "audit_manager");
        var auditor = TaskSeed.AddUser(db, "auditor");

        var finding = new AuditFinding { Code = "B-TSK-001", Title = "Bulgu", AuditorId = auditor.Id, Status = "closure_requested" };
        db.AuditFindings.Add(finding); db.SaveChanges();
        db.ClosureRequests.Add(new ClosureRequest { FindingId = finding.Id, Description = "Kapatma", RequestedById = auditor.Id, Status = "pending" });
        db.SaveChanges();

        var tasks = svc.GetTasksForUser(mgr.Id, "audit_manager");

        Assert.Contains(tasks, t => t.Category == "Kapanış Onayı");
    }

    // ── Admin her şeyi görür ─────────────────────────────────────────────────

    [Fact]
    public void Admin_SeesAllCategoryTypes()
    {
        var (db, svc) = TaskSeed.Build();
        var admin   = TaskSeed.AddUser(db, "admin");
        var auditor = TaskSeed.AddUser(db, "auditor");

        TaskSeed.AddRisk(db, admin.Id, "proposed");
        TaskSeed.AddRisk(db, admin.Id, "awaiting_approval");

        var finding = new AuditFinding { Code = "B-ADM-001", Title = "Bulgu", AuditorId = auditor.Id, Status = "closure_requested" };
        db.AuditFindings.Add(finding); db.SaveChanges();
        db.ClosureRequests.Add(new ClosureRequest { FindingId = finding.Id, Description = "Kapatma", RequestedById = auditor.Id, Status = "pending" });
        db.SaveChanges();

        var tasks = svc.GetTasksForUser(admin.Id, "admin");
        var categories = tasks.Select(t => t.Category).Distinct().ToHashSet();

        Assert.Contains("Risk İnceleme",  categories);
        Assert.Contains("Risk Onayı",     categories);
        Assert.Contains("Kapanış Onayı",  categories);
    }
}
