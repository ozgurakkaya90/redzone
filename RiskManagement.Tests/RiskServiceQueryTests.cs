using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

file static class QSeed
{
    public static (AppDbContext db, ConfigService cfg, RiskService svc) Build()
    {
        var db   = TestDb.Create();
        var cfg  = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var calc = new RiskCalculator(cfg);
        var svc  = new RiskService(db, calc);
        return (db, cfg, svc);
    }

    public static User AddUser(AppDbContext db, string role)
    {
        var u = new User { Username = $"{role}_{Guid.NewGuid():N}".Substring(0,20), FullName = role, Role = role, Department = role };
        db.Users.Add(u); db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleName = role });
        db.SaveChanges();
        return u;
    }

    public static Risk AddRisk(AppDbContext db, int proposedById, string status = "proposed",
        string? category = null, int? deptId = null, int? ownerId = null)
    {
        var r = new Risk
        {
            Code = $"R-Q-{Guid.NewGuid():N}".Substring(0,12), Title = "Query Risk",
            Status = status, Category = category, ProposedById = proposedById,
            OwnerId = ownerId, DepartmentId = deptId
        };
        db.Risks.Add(r); db.SaveChanges(); return r;
    }
}

// ── GetAll filtreleri ────────────────────────────────────────────────────────

public class RiskServiceGetAllTests
{
    [Fact]
    public void GetAll_NoFilter_ReturnsAll()
    {
        var (db, _, svc) = QSeed.Build();
        var u = QSeed.AddUser(db, "admin");
        QSeed.AddRisk(db, u.Id, "proposed");
        QSeed.AddRisk(db, u.Id, "approved");

        Assert.Equal(2, svc.GetAll().Count);
    }

    [Fact]
    public void GetAll_StatusFilter_ReturnsOnlyMatching()
    {
        var (db, _, svc) = QSeed.Build();
        var u = QSeed.AddUser(db, "admin");
        QSeed.AddRisk(db, u.Id, "proposed");
        QSeed.AddRisk(db, u.Id, "approved");
        QSeed.AddRisk(db, u.Id, "approved");

        var proposed = svc.GetAll(status: "proposed");
        var approved = svc.GetAll(status: "approved");

        Assert.Single(proposed);
        Assert.Equal(2, approved.Count);
    }

    [Fact]
    public void GetAll_CategoryFilter_ReturnsOnlyMatching()
    {
        var (db, _, svc) = QSeed.Build();
        var u = QSeed.AddUser(db, "admin");
        QSeed.AddRisk(db, u.Id, category: "Finansal");
        QSeed.AddRisk(db, u.Id, category: "Operasyonel");
        QSeed.AddRisk(db, u.Id, category: "Finansal");

        Assert.Equal(2, svc.GetAll(category: "Finansal").Count);
        Assert.Single(svc.GetAll(category: "Operasyonel"));
    }

    [Fact]
    public void GetAll_SearchFilter_MatchesTitleAndCode()
    {
        var (db, _, svc) = QSeed.Build();
        var u = QSeed.AddUser(db, "admin");
        var r = QSeed.AddRisk(db, u.Id);
        r.Title = "Parola Politikası Eksikliği"; db.SaveChanges();

        Assert.Single(svc.GetAll(search: "Parola"));
        Assert.Empty(svc.GetAll(search: "xyz_bulunamaz"));
    }

    [Fact]
    public void GetForUser_Admin_SeesAll()
    {
        var (db, _, svc) = QSeed.Build();
        var admin = QSeed.AddUser(db, "admin");
        var other = QSeed.AddUser(db, "user");
        QSeed.AddRisk(db, other.Id);
        QSeed.AddRisk(db, other.Id);

        Assert.Equal(2, svc.GetForUser(admin.Id, "admin").Count);
    }

    [Fact]
    public void GetForUser_User_SeesOnlyOwnRisks()
    {
        var (db, _, svc) = QSeed.Build();
        var u1 = QSeed.AddUser(db, "user");
        var u2 = QSeed.AddUser(db, "user");
        QSeed.AddRisk(db, u1.Id);
        QSeed.AddRisk(db, u2.Id);

        Assert.Single(svc.GetForUser(u1.Id, "user"));
    }

    [Fact]
    public void GetForUser_Owner_SeesOwnedRisks()
    {
        var (db, _, svc) = QSeed.Build();
        var mgr   = QSeed.AddUser(db, "risk_manager");
        var owner = QSeed.AddUser(db, "risk_owner");
        QSeed.AddRisk(db, mgr.Id, ownerId: owner.Id);
        QSeed.AddRisk(db, mgr.Id); // farklı risk — owner yok

        Assert.Single(svc.GetForUser(owner.Id, "risk_owner"));
    }

    [Fact]
    public void GetForUser_User_RiskWithNoDept_NotVisible()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "user");
        var mgr  = QSeed.AddUser(db, "risk_manager");
        // DepartmentId null — kullanıcının erişimi olmamalı
        QSeed.AddRisk(db, mgr.Id, deptId: null);

        Assert.Empty(svc.GetForUser(u.Id, "user"));
    }

    [Fact]
    public void GetById_ExistingRisk_ReturnsRisk()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "admin");
        var risk = QSeed.AddRisk(db, u.Id);

        Assert.NotNull(svc.GetById(risk.Id));
        Assert.Null(svc.GetById(99999));
    }
}

// ── UpdateRiskFields ─────────────────────────────────────────────────────────

public class RiskServiceUpdateFieldsTests
{
    [Fact]
    public void UpdateRiskFields_UpdatesAllFields()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id);

        var ok = svc.UpdateRiskFields(risk.Id, "external", "Tedarikçi", "Yangın",
            "İş durması", null, "KVKK Md.12", null, null, null, "Gözlemlendi", u.Id, "Operasyonel");

        Assert.True(ok);
        var updated = db.Risks.Find(risk.Id)!;
        Assert.Equal("external", updated.SourceType);
        Assert.Equal("Tedarikçi", updated.Source);
        Assert.Equal("Yangın", updated.Hazard);
        Assert.Equal("Operasyonel", updated.Category);
    }

    [Fact]
    public void UpdateRiskFields_NonExistentRisk_ReturnsFalse()
    {
        var (db, _, svc) = QSeed.Build();
        Assert.False(svc.UpdateRiskFields(99999, "internal", null, null, null, null, null, null, null, null, null));
    }

    [Fact]
    public void UpdateRiskFields_LogsChanges()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id);

        svc.UpdateRiskFields(risk.Id, "external", "Kaynak", null, null, null, null,
            null, null, null, null, u.Id);

        Assert.Contains(db.RiskAuditLogs, l => l.RiskId == risk.Id && l.Action.Contains("Güncellendi"));
    }

    [Fact]
    public void UpdateRiskFields_NoChanges_StillLogsUpdate()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id);

        svc.UpdateRiskFields(risk.Id, "internal", null, null, null, null, null,
            null, null, null, null, u.Id);

        Assert.Contains(db.RiskAuditLogs, l => l.RiskId == risk.Id);
    }
}

// ── Control ve Action CRUD ───────────────────────────────────────────────────

public class RiskControlCrudTests
{
    [Fact]
    public void EditControl_UpdatesFields()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id, "strategy_set");
        var ctrl = svc.AddControl(risk.Id, "Eski açıklama", "Önleyici", null, null, u.Id);

        var ok = svc.EditControl(risk.Id, ctrl.Id, "Yeni açıklama", "Tespit Edici", "Tatmin Edici", "Aylık", null, u.Id);

        Assert.True(ok);
        var updated = db.Controls.Find(ctrl.Id)!;
        Assert.Equal("Yeni açıklama", updated.Description);
        Assert.Equal("Tespit Edici", updated.ControlType);
    }

    [Fact]
    public void EditControl_WrongRiskId_ReturnsFalse()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id, "strategy_set");
        var ctrl = svc.AddControl(risk.Id, "Kontrol", "Önleyici", null, null, u.Id);

        Assert.False(svc.EditControl(99999, ctrl.Id, "X", "Önleyici", null, null, null));
    }

    [Fact]
    public void DeleteControl_RemovesFromDb()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id, "controlled");
        var ctrl = svc.AddControl(risk.Id, "Silinecek", "Önleyici", null, null, u.Id);

        Assert.True(svc.DeleteControl(risk.Id, ctrl.Id, u.Id));
        Assert.Null(db.Controls.Find(ctrl.Id));
    }

    [Fact]
    public void DeleteControl_NonExistent_ReturnsFalse()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id, "controlled");

        Assert.False(svc.DeleteControl(risk.Id, 99999));
    }
}

public class RiskActionCrudTests
{
    [Fact]
    public void EditAction_UpdatesFields()
    {
        var (db, _, svc) = QSeed.Build();
        var u      = QSeed.AddUser(db, "risk_manager");
        var risk   = QSeed.AddRisk(db, u.Id, "residual_evaluated");
        var action = svc.AddAction(risk.Id, "Eski", "BT", null, u.Id);

        var ok = svc.EditAction(risk.Id, action.Id, "Yeni açıklama", null,
            DateOnly.FromDateTime(DateTime.Today.AddMonths(1)), u.Id);

        Assert.True(ok);
        Assert.Equal("Yeni açıklama", db.ActionPlans.Find(action.Id)!.Description);
    }

    [Fact]
    public void DeleteAction_RemovesFromDb()
    {
        var (db, _, svc) = QSeed.Build();
        var u      = QSeed.AddUser(db, "risk_manager");
        var risk   = QSeed.AddRisk(db, u.Id, "residual_evaluated");
        var action = svc.AddAction(risk.Id, "Silinecek", "IT", null, u.Id);

        Assert.True(svc.DeleteAction(risk.Id, action.Id, u.Id));
        Assert.Null(db.ActionPlans.Find(action.Id));
    }

    [Fact]
    public void DeleteAction_NonExistent_ReturnsFalse()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "risk_manager");
        var risk = QSeed.AddRisk(db, u.Id);

        Assert.False(svc.DeleteAction(risk.Id, 99999));
    }

    [Fact]
    public void UpdateActionStatus_Cancelled_DoesNotSetCompletedAt()
    {
        var (db, _, svc) = QSeed.Build();
        var u      = QSeed.AddUser(db, "risk_manager");
        var risk   = QSeed.AddRisk(db, u.Id, "action_planned");
        var action = svc.AddAction(risk.Id, "Test", "IT", null, u.Id);

        svc.UpdateActionStatus(risk.Id, action.Id, "cancelled", u.Id);

        Assert.Null(db.ActionPlans.Find(action.Id)!.CompletedAt);
    }
}

// ── Reviews ──────────────────────────────────────────────────────────────────

public class RiskReviewTests
{
    [Fact]
    public void AddReview_PersistsData()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "committee");
        var risk = QSeed.AddRisk(db, u.Id, "approved");

        var rv = svc.AddReview(risk.Id, DateTime.UtcNow, "Kabul edildi", "Toplantı notları", u.Id);

        Assert.NotNull(rv);
        Assert.Equal("Kabul edildi", db.RiskReviews.Find(rv.Id)!.Decision);
    }

    [Fact]
    public void DeleteReview_RemovesFromDb()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "committee");
        var risk = QSeed.AddRisk(db, u.Id, "approved");
        var rv   = svc.AddReview(risk.Id, DateTime.UtcNow, "Karar", null, u.Id);

        svc.DeleteReview(rv.Id, u.Id);

        Assert.Null(db.RiskReviews.Find(rv.Id));
    }

    [Fact]
    public void DeleteReview_NonExistent_DoesNotThrow()
    {
        var (db, _, svc) = QSeed.Build();
        var ex = Record.Exception(() => svc.DeleteReview(99999));
        Assert.Null(ex);
    }

    [Fact]
    public void AddReview_LogsAction()
    {
        var (db, _, svc) = QSeed.Build();
        var u    = QSeed.AddUser(db, "committee");
        var risk = QSeed.AddRisk(db, u.Id, "approved");

        svc.AddReview(risk.Id, DateTime.UtcNow, "Karar", "Not", u.Id);

        Assert.Contains(db.RiskAuditLogs, l =>
            l.RiskId == risk.Id && l.Action.Contains("Gözden Geçirme"));
    }
}

// ── Dashboard & Stats ────────────────────────────────────────────────────────

public class RiskDashboardTests
{
    [Fact]
    public void GetDashboardStats_CountsCorrectly()
    {
        var (db, _, svc) = QSeed.Build();
        var u = QSeed.AddUser(db, "admin");
        QSeed.AddRisk(db, u.Id, "proposed");
        QSeed.AddRisk(db, u.Id, "proposed");
        QSeed.AddRisk(db, u.Id, "approved");
        QSeed.AddRisk(db, u.Id, "rejected");
        QSeed.AddRisk(db, u.Id, "controlled");

        var stats = svc.GetDashboardStats();

        Assert.Equal(5, stats.Total);
        Assert.Equal(2, stats.Proposed);
        Assert.Equal(1, stats.Approved);
        Assert.Equal(1, stats.Rejected);
        Assert.Equal(1, stats.Controlled);
    }

    [Fact]
    public void GetDashboardStats_EmptyDb_AllZero()
    {
        var (_, _, svc) = QSeed.Build();
        var stats = svc.GetDashboardStats();
        Assert.Equal(0, stats.Total);
    }

    [Fact]
    public void GetRadarData_GroupsByCategory()
    {
        var (db, cfg, svc) = QSeed.Build();
        var u = QSeed.AddUser(db, "admin");
        var r1 = QSeed.AddRisk(db, u.Id, category: "Finansal");
        var r2 = QSeed.AddRisk(db, u.Id, category: "Finansal");
        var r3 = QSeed.AddRisk(db, u.Id, category: "Operasyonel");

        // Değerlendirme ekle
        db.Evaluations.Add(new Evaluation { RiskId = r1.Id, EvalType = "initial", Probability = 3, Exposure = 2, Consequence = 15, Score = 90, RiskLevel = "Önemli Risk", EvaluatedById = u.Id });
        db.Evaluations.Add(new Evaluation { RiskId = r2.Id, EvalType = "initial", Probability = 1, Exposure = 1, Consequence = 7, Score = 7, RiskLevel = "Kabul Edilebilir", EvaluatedById = u.Id });
        db.SaveChanges();

        var radar = svc.GetRadarData();

        Assert.Equal(2, radar.Count); // Finansal + Operasyonel
        var finansal = radar.First(x => x.Category == "Finansal");
        Assert.Equal(2, finansal.Count);
        Assert.True(finansal.AvgInitial > 0);
    }

    [Fact]
    public void GetAdjacentIds_ReturnsCorrectPrevNext()
    {
        var (db, _, svc) = QSeed.Build();
        var admin = QSeed.AddUser(db, "admin");
        var r1 = QSeed.AddRisk(db, admin.Id);
        var r2 = QSeed.AddRisk(db, admin.Id);
        var r3 = QSeed.AddRisk(db, admin.Id);

        var (prev2, next2) = svc.GetAdjacentIds(r2.Id, admin.Id, "admin");

        // r3 en yeni (desc sıra), r1 en eski — r2 ortada
        Assert.NotNull(prev2);
        Assert.NotNull(next2);
    }

    [Fact]
    public void GetAdjacentIds_NonExistentRisk_ReturnsNulls()
    {
        var (_, _, svc) = QSeed.Build();
        var (prev, next) = svc.GetAdjacentIds(99999, 1, "admin");
        Assert.Null(prev);
        Assert.Null(next);
    }
}
