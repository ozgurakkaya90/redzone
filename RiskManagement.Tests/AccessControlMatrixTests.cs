using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// Erişim-kontrol matrisi testleri.
///
/// Temel kural: her (rol × kaynak × eylem) kombinasyonu için liste / detay / dışa aktarım
/// tutarlı olmalıdır. Bu testler, önceki tutarsızlıkların (audit.read risk genişlemesi,
/// ethics export sızıntısı) bir daha kaçmamasını güvence altına alır.
/// </summary>
public class AccessControlMatrixTests
{
    // ── Risk ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// BuildUserQueryForUser (liste/export) ile CanAccessRiskForUser (detay) tutarlı olmalı.
    /// Denetçi, kendi departmanı dışındaki bir riski ne listede görmeli ne de detayda açabilmeli.
    /// </summary>
    [Fact]
    public void Auditor_CannotSee_OutOfDeptRisk_InListOrDetail()
    {
        using var db = TestDb.Create();
        var authSvc = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var cfgSvc = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var svc = new RiskService(db, new RiskCalculator(cfgSvc), authSvc);

        db.Departments.AddRange(
            new Department { Id = 10, Name = "BilgiİşlemDept" },
            new Department { Id = 20, Name = "FinansDept" });
        db.Users.Add(new User
        {
            Id = 1, Username = "denetci", FullName = "Denetçi",
            Role = "auditor", DepartmentId = 10
        });
        db.UserRoles.Add(new UserRole { UserId = 1, RoleName = "auditor" });
        db.UserDepartments.Add(new UserDepartment { UserId = 1, DepartmentId = 10 });

        var outOfDeptRisk = new Risk
        {
            Id = 100, Code = "R-OUTD-001", Title = "Dışarı risk",
            DepartmentId = 20, ProposedById = 99, OwnerId = 99
        };
        db.Risks.Add(outOfDeptRisk);
        db.SaveChanges();

        var auditorUser = db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .First(u => u.Id == 1);

        // Liste/export — denetçi dışarıdaki riski görmemeli
        var list = svc.GetForUser(auditorUser);
        Assert.DoesNotContain(list, r => r.Id == 100);

        // Detay — denetçi dışarıdaki riski açamamalı
        Assert.False(svc.CanAccessRiskForUser(outOfDeptRisk, auditorUser));
    }

    /// <summary>
    /// risk.manage iznine sahip roller (admin, committee, risk_manager, audit_manager) tüm riskleri görür.
    /// </summary>
    [Fact]
    public void RiskManager_CanSeeAllRisks_InListAndDetail()
    {
        using var db = TestDb.Create();
        var authSvc = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var cfgSvc = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var svc = new RiskService(db, new RiskCalculator(cfgSvc), authSvc);

        db.Departments.Add(new Department { Id = 30, Name = "BaşkaDept" });
        db.Users.Add(new User
        {
            Id = 2, Username = "riskMgr", FullName = "Risk Müdürü",
            Role = "risk_manager", DepartmentId = 10
        });
        db.UserRoles.Add(new UserRole { UserId = 2, RoleName = "risk_manager" });

        var remoteRisk = new Risk
        {
            Id = 200, Code = "R-REMOTE-001", Title = "Uzak risk",
            DepartmentId = 30, ProposedById = 99, OwnerId = 99
        };
        db.Risks.Add(remoteRisk);
        db.SaveChanges();

        var mgrUser = db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .First(u => u.Id == 2);

        var list = svc.GetForUser(mgrUser);
        Assert.Contains(list, r => r.Id == 200);
        Assert.True(svc.CanAccessRiskForUser(remoteRisk, mgrUser));
    }

    /// <summary>
    /// Aynı departmandaki risk sahibi hem listede görür hem detayı açabilir.
    /// Farklı departmandaki risk sahibi ise hiçbirini yapamaz.
    /// </summary>
    [Fact]
    public void RiskOwner_SeesDeptRisk_NotOtherDeptRisk()
    {
        using var db = TestDb.Create();
        var authSvc = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var cfgSvc = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var svc = new RiskService(db, new RiskCalculator(cfgSvc), authSvc);

        db.Departments.AddRange(
            new Department { Id = 10, Name = "DeptA" },
            new Department { Id = 11, Name = "DeptB" });
        db.Users.Add(new User
        {
            Id = 3, Username = "owner", FullName = "Sahip",
            Role = "risk_owner", DepartmentId = 10
        });
        db.UserRoles.Add(new UserRole { UserId = 3, RoleName = "risk_owner" });
        db.UserDepartments.Add(new UserDepartment { UserId = 3, DepartmentId = 10 });

        db.Risks.AddRange(
            new Risk { Id = 301, Code = "R-A-001", Title = "A riski", DepartmentId = 10, ProposedById = 99, OwnerId = 99 },
            new Risk { Id = 302, Code = "R-B-001", Title = "B riski", DepartmentId = 11, ProposedById = 99, OwnerId = 99 });
        db.SaveChanges();

        var owner = db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .First(u => u.Id == 3);

        var list = svc.GetForUser(owner);
        Assert.Contains(list, r => r.Id == 301);
        Assert.DoesNotContain(list, r => r.Id == 302);
    }

    // ── Erişim kapı tutarlılığı (liste ↔ detay) ──────────────────────────────

    /// <summary>
    /// BuildUserQueryForUser ile CanAccessRiskForUser aynı kuralı uygulamalıdır.
    /// Bir riskin listede görünmesi, detayda açılabilmesini garanti etmeli.
    /// </summary>
    [Fact]
    public void ListAndDetail_AccessGates_AreConsistent()
    {
        using var db = TestDb.Create();
        var authSvc = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var cfgSvc = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var svc = new RiskService(db, new RiskCalculator(cfgSvc), authSvc);

        db.Departments.Add(new Department { Id = 10, Name = "DeptA" });
        db.Users.Add(new User
        {
            Id = 1, Username = "testuser", FullName = "Test User",
            Role = "user", DepartmentId = 10
        });
        db.UserRoles.Add(new UserRole { UserId = 1, RoleName = "user" });
        db.UserDepartments.Add(new UserDepartment { UserId = 1, DepartmentId = 10 });

        var r1 = new Risk { Id = 1, Code = "R-01", Title = "Erişilebilir", DepartmentId = 10, ProposedById = 99, OwnerId = 99 };
        var r2 = new Risk { Id = 2, Code = "R-02", Title = "Erişilemez",  DepartmentId = 20, ProposedById = 99, OwnerId = 99 };
        db.Risks.AddRange(r1, r2);
        db.SaveChanges();

        var user = db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .First(u => u.Id == 1);

        var listIds = svc.GetForUser(user).Select(r => r.Id).ToHashSet();

        // Listede görünen her risk detayda da açılabilmeli
        Assert.True(svc.CanAccessRiskForUser(r1, user));
        Assert.Contains(1, listIds);

        // Listede görünmeyen risk detayda da açılamamalı
        Assert.False(svc.CanAccessRiskForUser(r2, user));
        Assert.DoesNotContain(2, listIds);
    }

    // ── ReviewClosureRequest whitelist ────────────────────────────────────────

    [Fact]
    public void ReviewClosureRequest_RejectsUnknownDecision()
    {
        using var db = TestDb.Create();
        var svc = new AuditService(db);

        db.AuditFindings.Add(new AuditFinding
        {
            Id = 1, Code = "B-TEST-001", Title = "Test finding", Status = "closure_requested"
        });
        db.ClosureRequests.Add(new ClosureRequest
        {
            Id = 1, FindingId = 1, Description = "Kapatılsın", RequestedById = 2
        });
        db.SaveChanges();

        // "approved"/"rejected" dışı bir değer reddedilmeli
        var result = svc.InternalReviewClosureRequest(1, 1, "force_close", null, 99);
        Assert.False(result);

        // Bulgu durumu değişmemiş olmalı
        var finding = db.AuditFindings.Find(1);
        Assert.Equal("closure_requested", finding?.Status);
    }

    [Fact]
    public void ReviewClosureRequest_AcceptsValidDecisions()
    {
        using var db = TestDb.Create();
        var svc = new AuditService(db);

        db.AuditFindings.Add(new AuditFinding
        {
            Id = 2, Code = "B-TEST-002", Title = "Approved finding", Status = "closure_requested"
        });
        db.ClosureRequests.Add(new ClosureRequest
        {
            Id = 2, FindingId = 2, Description = "Kapanış", RequestedById = 3
        });
        db.SaveChanges();

        var result = svc.InternalReviewClosureRequest(2, 2, "approved", null, 99);
        Assert.True(result);

        var finding = db.AuditFindings.Find(2);
        Assert.Equal(FindingStatus.Closed, finding?.Status);
    }

    // ── Etik bildirim export erişim tutarlılığı ────────────────────────────────

    /// <summary>
    /// Denetçi (auditor) ethics.read iznine sahip olsa da etik export için
    /// ethics.audit_review VEYA ethics.board_review gerekiyor — bu testle
    /// default izin tablosunun buna göre yapılandırıldığı doğrulanır.
    /// </summary>
    [Fact]
    public void Auditor_HasEthicsRead_ButNot_ReviewPerms()
    {
        var auditorPerms = AuthService.DefaultPermissions["auditor"];
        Assert.Contains("ethics.read", auditorPerms);
        Assert.DoesNotContain("ethics.audit_review", auditorPerms);
        Assert.DoesNotContain("ethics.board_review", auditorPerms);
    }

    [Fact]
    public void EthicsExportGate_MatchesViewPageRoles()
    {
        // Export gate: ethics.audit_review || ethics.board_review
        // UI sayfaları: audit_manager, ethics_board, admin

        // audit_manager → ethics.audit_review ✓
        Assert.Contains("ethics.audit_review", AuthService.DefaultPermissions["audit_manager"]);

        // ethics_board → ethics.board_review ✓
        Assert.Contains("ethics.board_review", AuthService.DefaultPermissions["ethics_board"]);

        // admin → her izne sahip
        Assert.Contains("ethics.audit_review", AuthService.DefaultPermissions["admin"]);
        Assert.Contains("ethics.board_review", AuthService.DefaultPermissions["admin"]);

        // auditor → hiçbir review iznine sahip değil → export erişimi yok ✓
        Assert.DoesNotContain("ethics.audit_review", AuthService.DefaultPermissions["auditor"]);
        Assert.DoesNotContain("ethics.board_review", AuthService.DefaultPermissions["auditor"]);
    }

    // ── Bulgu görüntüleme ↔ dosya indirme asimetrisi (bilinçli) ───────────────

    private static ClaimsPrincipal Principal(int userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    /// <summary>
    /// Bilinçli güvenlik kararını sabitler: bulguyu departman üyeliğiyle <b>görebilen</b>
    /// bir kullanıcı, ek/kanıt dosyalarını <b>indiremez</b>. İndirme yalnızca admin/audit_manager,
    /// bulgunun denetçisi ve finding_owner rolündeki atanan sahibe açıktır.
    /// Bu asimetri değişirse (örn. departman erişimi indirmeye genişletilirse) bu test kırılır.
    /// </summary>
    [Fact]
    public void DeptMember_CanView_ButCannotDownload_FindingFile()
    {
        using var db = TestDb.Create();
        var svc = new AuditService(db);

        db.Departments.Add(new Department { Id = 40, Name = "DenetlenenDept" });
        // Departman üyesi (rolü "user"), bulgunun sahibi/denetçisi DEĞİL.
        db.Users.Add(new User { Id = 5, Username = "deptuser", FullName = "Dept Üye", Role = "user", DepartmentId = 40 });
        db.UserDepartments.Add(new UserDepartment { UserId = 5, DepartmentId = 40 });

        var finding = new AuditFinding
        {
            Id = 50, Code = "B-DL-001", Title = "Kanıtlı bulgu",
            DepartmentId = 40, AuditorId = 91, OwnerId = 92, Status = "open"
        };
        db.AuditFindings.Add(finding);
        db.SaveChanges();

        // Görüntüleme: departman üyeliğiyle erişebilir
        Assert.True(svc.CanAccessFinding(finding, userId: 5, role: "user"));

        // İndirme: departman üyeliği yeterli DEĞİL (kasıtlı olarak daha katı)
        Assert.False(AuditService.CanDownloadFindingFile(Principal(5, "user"), finding));

        // Denetçi ve finding_owner sahip indirebilir
        Assert.True(AuditService.CanDownloadFindingFile(Principal(91, "auditor"), finding));
        Assert.True(AuditService.CanDownloadFindingFile(Principal(92, "finding_owner"), finding));

        // audit_manager her zaman indirebilir
        Assert.True(AuditService.CanDownloadFindingFile(Principal(99, "audit_manager"), finding));
    }
}
