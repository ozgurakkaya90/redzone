using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

// ─── Yardımcılar ────────────────────────────────────────────────────────────

file static class RBase
{
    public static (AppDbContext db, AuthService auth) Auth()
    {
        var db = TestDb.Create();
        return (db, new AuthService(db, new MemoryCache(new MemoryCacheOptions())));
    }

    public static User AddUser(AppDbContext db, string role, string? email = null, bool active = true)
    {
        var u = new User
        {
            Username     = $"{role}_{Guid.NewGuid():N}"[..20],
            FullName     = role,
            Role         = role,
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test123!"),
            IsActive     = active,
        };
        db.Users.Add(u);
        db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleName = role });
        db.SaveChanges();
        return u;
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 1. AffectedPersons yardımcı metotları
// ════════════════════════════════════════════════════════════════════════════

public class AffectedPersonsTests
{
    [Fact]
    public void GetAffectedPersonsList_NullField_ReturnsEmptyList()
    {
        var risk = new Risk { AffectedPersons = null };
        Assert.Empty(risk.GetAffectedPersonsList());
    }

    [Fact]
    public void GetAffectedPersonsList_EmptyString_ReturnsEmptyList()
    {
        var risk = new Risk { AffectedPersons = "" };
        Assert.Empty(risk.GetAffectedPersonsList());
    }

    [Fact]
    public void GetAffectedPersonsList_ValidJson_ReturnsItems()
    {
        var risk = new Risk();
        risk.SetAffectedPersonsList(["Operatör", "Teknisyen", "Mühendis"]);

        var result = risk.GetAffectedPersonsList();

        Assert.Equal(3, result.Count);
        Assert.Contains("Operatör", result);
        Assert.Contains("Teknisyen", result);
        Assert.Contains("Mühendis", result);
    }

    [Fact]
    public void SetAffectedPersonsList_WhitespaceItems_AreFiltered()
    {
        var risk = new Risk();
        risk.SetAffectedPersonsList(["Operatör", "  ", "", "Mühendis"]);

        var result = risk.GetAffectedPersonsList();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void SetAffectedPersonsList_EmptyList_SetsNull()
    {
        var risk = new Risk { AffectedPersons = "[\"Operatör\"]" };
        risk.SetAffectedPersonsList([]);

        Assert.Null(risk.AffectedPersons);
    }

    [Fact]
    public void SetAffectedPersonsList_OnlyWhitespace_SetsNull()
    {
        var risk = new Risk();
        risk.SetAffectedPersonsList(["  ", "\t"]);

        Assert.Null(risk.AffectedPersons);
    }

    [Fact]
    public void SerializePersonsList_Static_ProducesSameResultAsInstance()
    {
        var items = new[] { "Operatör", "Teknisyen" };
        var risk  = new Risk();
        risk.SetAffectedPersonsList(items);

        var staticResult   = Risk.SerializePersonsList(items);
        var instanceResult = risk.AffectedPersons;

        Assert.Equal(staticResult, instanceResult);
    }

    [Fact]
    public void GetAffectedPersonsList_RoundTrip_IsIdempotent()
    {
        var original = new[] { "Operatör", "İdari Personel", "Müdür" };
        var risk = new Risk();
        risk.SetAffectedPersonsList(original);

        var retrieved = risk.GetAffectedPersonsList();
        risk.SetAffectedPersonsList(retrieved);

        Assert.Equal(retrieved, risk.GetAffectedPersonsList());
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 2. AnonymousRateLimiter
// ════════════════════════════════════════════════════════════════════════════

public class AnonymousRateLimiterTests
{
    private static AnonymousRateLimiter Build() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void IsAllowed_FirstRequest_ReturnsTrue()
    {
        var limiter = Build();
        Assert.True(limiter.IsAllowed("192.168.1.1"));
    }

    [Fact]
    public void IsAllowed_NullIp_AlwaysReturnsTrue()
    {
        var limiter = Build();
        for (int i = 0; i < 10; i++)
            Assert.True(limiter.IsAllowed(null));
    }

    [Fact]
    public void IsAllowed_EmptyIp_AlwaysReturnsTrue()
    {
        var limiter = Build();
        for (int i = 0; i < 10; i++)
            Assert.True(limiter.IsAllowed(""));
    }

    [Fact]
    public void IsAllowed_AfterMaxAttempts_ReturnsFalse()
    {
        var limiter = Build();
        const string ip = "10.0.0.1";

        // MaxAttempts = 5
        for (int i = 0; i < 5; i++)
            limiter.IsAllowed(ip);

        Assert.False(limiter.IsAllowed(ip));
    }

    [Fact]
    public void IsAllowed_DifferentIps_AreTrackedIndependently()
    {
        var limiter = Build();

        for (int i = 0; i < 5; i++)
            limiter.IsAllowed("10.0.0.1");

        // Farklı IP kısıtlanmamış olmalı
        Assert.True(limiter.IsAllowed("10.0.0.2"));
        Assert.False(limiter.IsAllowed("10.0.0.1"));
    }

    [Fact]
    public void IsAllowed_ExactlyAtLimit_StillReturnsTrue()
    {
        var limiter = Build();
        const string ip = "172.16.0.5";

        // 4. deneme hâlâ geçerli; 5. deneme (MaxAttempts = 5) sınırı doldurur
        for (int i = 0; i < 4; i++)
            Assert.True(limiter.IsAllowed(ip));

        // 5. deneme: count == MaxAttempts-1 → izin ver, sonra artır
        Assert.True(limiter.IsAllowed(ip));

        // 6. deneme: count == MaxAttempts → engel
        Assert.False(limiter.IsAllowed(ip));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 3. DefaultPermissions doğruluğu (W-5, W-6)
// ════════════════════════════════════════════════════════════════════════════

public class DefaultPermissionsTests
{
    [Fact]
    public void RiskOwner_HasRiskModify_ForEvaluationAndStrategy()
    {
        var perms = AuthService.DefaultPermissions[Roles.RiskOwner];
        Assert.Contains("risk.modify", perms);
    }

    [Fact]
    public void Committee_HasControlRead_ForRiskReview()
    {
        var perms = AuthService.DefaultPermissions[Roles.Committee];
        Assert.Contains("control.read", perms);
    }

    [Fact]
    public void Committee_HasActionRead_ForRiskReview()
    {
        var perms = AuthService.DefaultPermissions[Roles.Committee];
        Assert.Contains("action.read", perms);
    }

    [Fact]
    public void Admin_HasAllPermissions()
    {
        var adminPerms = new HashSet<string>(AuthService.DefaultPermissions[Roles.Admin]);
        foreach (var perm in AuthService.Permissions.Keys)
            Assert.Contains(perm, adminPerms);
    }

    [Fact]
    public void AllRoles_HaveAtLeastOnePermission()
    {
        foreach (var role in Roles.All)
        {
            Assert.True(AuthService.DefaultPermissions.ContainsKey(role), $"Rol '{role}' DefaultPermissions'da eksik");
            Assert.NotEmpty(AuthService.DefaultPermissions[role]);
        }
    }

    [Fact]
    public void EthicsBoard_DoesNotHaveRiskPermissions()
    {
        var perms = AuthService.DefaultPermissions[Roles.EthicsBoard];
        Assert.DoesNotContain("risk.write", perms);
        Assert.DoesNotContain("risk.modify", perms);
        Assert.DoesNotContain("audit.write", perms);
    }

    [Fact]
    public void FindingOwner_CanModifyButNotCreate()
    {
        var perms = AuthService.DefaultPermissions[Roles.FindingOwner];
        Assert.Contains("audit.modify", perms);
        Assert.DoesNotContain("audit.write", perms);
    }

    [Fact]
    public void RiskManager_HasFullRiskAndControlPermissions()
    {
        var perms = new HashSet<string>(AuthService.DefaultPermissions[Roles.RiskManager]);
        Assert.Contains("risk.read",      perms);
        Assert.Contains("risk.write",     perms);
        Assert.Contains("risk.modify",    perms);
        Assert.Contains("control.read",   perms);
        Assert.Contains("control.write",  perms);
        Assert.Contains("control.modify", perms);
        Assert.Contains("action.read",    perms);
        Assert.Contains("action.write",   perms);
        Assert.Contains("action.modify",  perms);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 4. AuthService.AnonymizeUser
// ════════════════════════════════════════════════════════════════════════════

public class AnonymizeUserTests
{
    [Fact]
    public async Task AnonymizeUser_ClearsPersonalData()
    {
        var (db, auth) = RBase.Auth();
        var user = RBase.AddUser(db, "user", email: "ali@example.com");

        await auth.AnonymizeUserAsync(user.Id);

        var after = db.Users.Find(user.Id)!;
        Assert.Equal("[Silinmiş Kullanıcı]", after.FullName);
        Assert.Null(after.Email);
        Assert.Null(after.PasswordHash);
        Assert.False(after.IsActive);
    }

    [Fact]
    public async Task AnonymizeUser_Username_StartsWithAnonPrefix()
    {
        var (db, auth) = RBase.Auth();
        var user = RBase.AddUser(db, "risk_owner");

        await auth.AnonymizeUserAsync(user.Id);

        var after = db.Users.Find(user.Id)!;
        Assert.StartsWith("anon_", after.Username);
    }

    [Fact]
    public async Task AnonymizeUser_LockoutSet_PreventsLogin()
    {
        var (db, auth) = RBase.Auth();
        var user = RBase.AddUser(db, "auditor");

        await auth.AnonymizeUserAsync(user.Id);

        var after = db.Users.Find(user.Id)!;
        Assert.NotNull(after.LockoutUntil);
        Assert.True(after.LockoutUntil > DateTime.UtcNow.AddYears(50));
    }

    [Fact]
    public async Task AnonymizeUser_LastAdmin_ThrowsInvalidOperation()
    {
        var (db, auth) = RBase.Auth();
        var admin = RBase.AddUser(db, "admin");

        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.AnonymizeUserAsync(admin.Id));
    }

    [Fact]
    public async Task AnonymizeUser_NotLastAdmin_Succeeds()
    {
        var (db, auth) = RBase.Auth();
        var admin1 = RBase.AddUser(db, "admin");
        var admin2 = RBase.AddUser(db, "admin");

        var ex = await Record.ExceptionAsync(() => auth.AnonymizeUserAsync(admin1.Id));
        Assert.Null(ex);
        Assert.False(db.Users.Find(admin1.Id)!.IsActive);
    }

    [Fact]
    public async Task AnonymizeUser_InvalidatesPasswordResetTokens()
    {
        var (db, auth) = RBase.Auth();
        var user = RBase.AddUser(db, "user");
        var rawToken = AuthService.GenerateResetToken();
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId    = user.Id,
            Token     = AuthService.HashResetToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            Used      = false,
        });
        db.SaveChanges();

        await auth.AnonymizeUserAsync(user.Id);

        var token = db.PasswordResetTokens.First(t => t.UserId == user.Id);
        Assert.True(token.Used);
    }

    [Fact]
    public async Task AnonymizeUser_NonExistent_ThrowsInvalidOperation()
    {
        var (_, auth) = RBase.Auth();
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.AnonymizeUserAsync(9999));
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 5. GetDashboardForUser scope doğrulaması
// ════════════════════════════════════════════════════════════════════════════

public class AuditDashboardScopeTests
{
    private static (AppDbContext db, AuditService svc) Build()
    {
        var db  = TestDb.Create();
        var svc = new AuditService(db);
        return (db, svc);
    }

    private static User AddUser(AppDbContext db, string role)
    {
        var u = new User { Username = $"{role}_{Guid.NewGuid():N}"[..20], FullName = role, Role = role };
        db.Users.Add(u); db.SaveChanges(); return u;
    }

    private static AuditFinding AddFinding(AppDbContext db, int auditorId,
        string status = "open", string? severity = null, int? deptId = null)
    {
        var f = new AuditFinding
        {
            Code        = $"B-S-{Guid.NewGuid():N}"[..10],
            Title       = "Scope Bulgu",
            AuditorId   = auditorId,
            Status      = status,
            Severity    = severity,
            DepartmentId = deptId,
            AuditSource = "internal",
        };
        db.AuditFindings.Add(f); db.SaveChanges(); return f;
    }

    [Fact]
    public void GetDashboard_CountsOpenAndClosed_Correctly()
    {
        var (db, svc) = Build();
        var auditor = AddUser(db, "auditor");

        AddFinding(db, auditor.Id, "open");
        AddFinding(db, auditor.Id, "open");
        AddFinding(db, auditor.Id, "closed");

        var stats = svc.GetDashboard([]);

        Assert.Equal(3, stats.Total);
        Assert.Equal(2, stats.Open);
        Assert.Equal(1, stats.Closed);
    }

    [Fact]
    public void GetDashboardForUser_Auditor_SeesOnlyOwnFindings()
    {
        var (db, svc) = Build();
        var auditor1 = AddUser(db, "auditor");
        var auditor2 = AddUser(db, "auditor");

        AddFinding(db, auditor1.Id, "open");
        AddFinding(db, auditor1.Id, "open");
        AddFinding(db, auditor2.Id, "open"); // farklı denetçi

        var stats = svc.GetDashboardForUser(auditor1.Id, "auditor", []);

        Assert.Equal(2, stats.Total);
    }

    [Fact]
    public void GetDashboardForUser_AuditManager_SeesAll()
    {
        var (db, svc) = Build();
        var auditor  = AddUser(db, "auditor");
        var manager  = AddUser(db, "audit_manager");

        AddFinding(db, auditor.Id, "open");
        AddFinding(db, auditor.Id, "closed");
        AddFinding(db, manager.Id, "open");

        var stats = svc.GetDashboardForUser(manager.Id, "audit_manager", []);

        Assert.Equal(3, stats.Total);
    }

    [Fact]
    public void GetDashboard_BySeverity_InitializesAllSeveritiesFromParameter()
    {
        var (db, svc) = Build();
        var auditor = AddUser(db, "auditor");

        AddFinding(db, auditor.Id, severity: "Kritik");
        AddFinding(db, auditor.Id, severity: "Kritik");
        AddFinding(db, auditor.Id, severity: "Yüksek");

        var severities = new[] { "Kritik", "Yüksek", "Orta", "Düşük" };
        var stats = svc.GetDashboard(severities);

        Assert.Equal(2, stats.BySeverity["Kritik"]);
        Assert.Equal(1, stats.BySeverity["Yüksek"]);
        Assert.Equal(0, stats.BySeverity["Orta"]);
        Assert.Equal(0, stats.BySeverity["Düşük"]);
    }

    [Fact]
    public void GetDashboard_OverdueFindings_OnlyCountsNonClosed()
    {
        var (db, svc) = Build();
        var auditor = AddUser(db, "auditor");

        var pastDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10));

        var f1 = AddFinding(db, auditor.Id, "open");
        f1.DueDate = pastDate; db.SaveChanges();

        var f2 = AddFinding(db, auditor.Id, "closed");
        f2.DueDate = pastDate; db.SaveChanges();

        var stats = svc.GetDashboard([]);

        Assert.Equal(1, stats.Overdue);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 6. GetRadarData — category bazlı skor hesaplama
// ════════════════════════════════════════════════════════════════════════════

public class GetRadarDataTests
{
    private static (AppDbContext db, RiskService svc) Build()
    {
        var db   = TestDb.Create();
        var cfg  = TestDb.CreateConfigService(db);
        var calc = new RiskCalculator(cfg);
        var auth = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var svc  = new RiskService(db, calc, auth);
        return (db, svc);
    }

    private static User AddUser(AppDbContext db)
    {
        var u = new User { Username = $"u_{Guid.NewGuid():N}"[..12], FullName = "U", Role = "admin" };
        db.Users.Add(u); db.SaveChanges(); return u;
    }

    private static Risk AddRisk(AppDbContext db, int userId, string category)
    {
        var r = new Risk
        {
            Code     = $"R-RD-{Guid.NewGuid():N}"[..12],
            Title    = "Radar Risk",
            Category = category,
            ProposedById = userId,
        };
        db.Risks.Add(r); db.SaveChanges(); return r;
    }

    private static void AddEval(AppDbContext db, int riskId, int userId,
        string evalType, double p, double e, double c)
    {
        var score = p * e * c;
        db.Evaluations.Add(new Evaluation
        {
            RiskId      = riskId,
            EvalType    = evalType,
            Probability = p, Exposure = e, Consequence = c,
            Score       = score,
            RiskLevel   = score >= 400 ? "Çok Yüksek Risk" : score >= 200 ? "Yüksek Risk" : "Orta Risk",
            EvaluatedById = userId,
        });
        db.SaveChanges();
    }

    [Fact]
    public void GetRadarData_EmptyDb_ReturnsEmptyList()
    {
        var (_, svc) = Build();
        Assert.Empty(svc.GetRadarData());
    }

    [Fact]
    public void GetRadarData_RisksWithNoCategory_NotIncluded()
    {
        var (db, svc) = Build();
        var u = AddUser(db);
        var r = new Risk { Code = "R-NC-001", Title = "No Cat", ProposedById = u.Id };
        db.Risks.Add(r); db.SaveChanges();

        Assert.Empty(svc.GetRadarData());
    }

    [Fact]
    public void GetRadarData_GroupsByCategory()
    {
        var (db, svc) = Build();
        var u = AddUser(db);

        AddRisk(db, u.Id, "Operasyonel");
        AddRisk(db, u.Id, "Operasyonel");
        AddRisk(db, u.Id, "Finansal");

        var result = svc.GetRadarData();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Category == "Operasyonel" && r.Count == 2);
        Assert.Contains(result, r => r.Category == "Finansal" && r.Count == 1);
    }

    [Fact]
    public void GetRadarData_CalculatesAverageInitialScore()
    {
        var (db, svc) = Build();
        var u = AddUser(db);

        var r1 = AddRisk(db, u.Id, "Operasyonel");
        var r2 = AddRisk(db, u.Id, "Operasyonel");

        AddEval(db, r1.Id, u.Id, "initial",  p: 6, e: 3, c: 7);  // score = 126
        AddEval(db, r2.Id, u.Id, "initial",  p: 3, e: 2, c: 7);  // score = 42

        var result = svc.GetRadarData();
        var cat = result.Single(r => r.Category == "Operasyonel");

        // avg = (126 + 42) / 2 = 84
        Assert.Equal(84.0, cat.AvgInitial, precision: 1);
    }

    [Fact]
    public void GetRadarData_NoEvaluations_ScoresAreZero()
    {
        var (db, svc) = Build();
        var u = AddUser(db);

        AddRisk(db, u.Id, "Stratejik");

        var result = svc.GetRadarData();
        var cat = result.Single();

        Assert.Equal(0, cat.AvgInitial);
        Assert.Equal(0, cat.MaxInitial);
        Assert.Equal(0, cat.AvgResidual);
        Assert.Null(cat.AvgReduction);
    }

    [Fact]
    public void GetRadarData_OrderedByAvgInitialDescending()
    {
        var (db, svc) = Build();
        var u = AddUser(db);

        var rA = AddRisk(db, u.Id, "CatA");
        var rB = AddRisk(db, u.Id, "CatB");

        AddEval(db, rA.Id, u.Id, "initial", 1, 1, 1);   // score = 1
        AddEval(db, rB.Id, u.Id, "initial", 10, 6, 40); // score = 2400

        var result = svc.GetRadarData();

        Assert.Equal("CatB", result[0].Category);
        Assert.Equal("CatA", result[1].Category);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// 7. RiskLibraryService — arama ve filtreleme
// ════════════════════════════════════════════════════════════════════════════

public class RiskLibraryServiceTests
{
    private static (AppDbContext db, RiskLibraryService svc) Build()
    {
        var db  = TestDb.Create();
        var svc = new RiskLibraryService(db);
        return (db, svc);
    }

    private static void AddItem(AppDbContext db, string title, string category,
        string? subCategory = null, bool active = true, string? tags = null)
    {
        var u = db.Users.FirstOrDefault()
            ?? new User { Username = "seed", FullName = "Seed", Role = "admin" };
        if (u.Id == 0) { db.Users.Add(u); db.SaveChanges(); }

        db.RiskLibraryItems.Add(new RiskLibraryItem
        {
            Title       = title,
            Category    = category,
            SubCategory = subCategory,
            IsActive    = active,
            CreatedById = u.Id,
            Tags        = tags,
        });
        db.SaveChanges();
    }

    [Fact]
    public void Search_NoFilter_ReturnsAllActive()
    {
        var (db, svc) = Build();
        AddItem(db, "Yangın Riski", "Operasyonel");
        AddItem(db, "Sızıntı Riski", "Çevre");
        AddItem(db, "Arşiv Riski", "Operasyonel", active: false); // pasif

        var result = svc.Search(null, null, null, null);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Search_CategoryFilter_ReturnsOnlyMatching()
    {
        var (db, svc) = Build();
        AddItem(db, "Yangın", "Operasyonel");
        AddItem(db, "Kaza",   "Operasyonel");
        AddItem(db, "Mali",   "Finansal");

        var result = svc.Search(null, "Operasyonel", null, null);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("Operasyonel", r.Category));
    }

    [Fact]
    public void Search_TextSearch_MatchesTitle()
    {
        var (db, svc) = Build();
        AddItem(db, "Yangın Riski", "Operasyonel");
        AddItem(db, "Mali Kayıp",   "Finansal");

        var result = svc.Search("yangın", null, null, null);

        Assert.Single(result);
        Assert.Equal("Yangın Riski", result[0].Title);
    }

    [Fact]
    public void Search_TextSearch_IsCaseInsensitive()
    {
        // InMemory provider, sütun ToLower()'ını ORTAM kültürüyle .NET'te değerlendirir;
        // production'da arama SQL'e (MySQL invariant LOWER) çevrilir. Türkçe kültürde InMemory
        // "Item" → "ıtem" (noktasız) yapıp testi yanıltıcı kırardı. Production davranışını
        // yansıtmak için invariant kültürde çalıştır. (Servis tarafı arama terimini zaten
        // ToLowerInvariant ile küçültür — bkz. RiskLibraryService.Search Türkçe-I notu.)
        var prev = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var (db, svc) = Build();
            AddItem(db, "Fire Risk Item", "Operasyonel");

            var lower = svc.Search("fire risk item", null, null, null);
            var upper = svc.Search("FIRE RISK", null, null, null);

            Assert.Single(lower);
            Assert.Single(upper);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = prev; }
    }

    [Fact]
    public void Search_TextSearch_MatchesTags()
    {
        var (db, svc) = Build();
        AddItem(db, "İş Güvenliği", "Operasyonel", tags: "isg, yangın, kaza");

        var result = svc.Search("isg", null, null, null);

        Assert.Single(result);
    }

    [Fact]
    public void Search_InactiveItems_ExcludedByDefault()
    {
        var (db, svc) = Build();
        AddItem(db, "Pasif Risk", "Operasyonel", active: false);

        var result = svc.Search(null, null, null, null);

        Assert.Empty(result);
    }

    [Fact]
    public void Search_InactiveItems_IncludedWhenOnlyActiveFalse()
    {
        var (db, svc) = Build();
        AddItem(db, "Aktif",  "Operasyonel", active: true);
        AddItem(db, "Pasif",  "Operasyonel", active: false);

        var result = svc.Search(null, null, null, null, onlyActive: false);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetCategories_ReturnsDistinctActiveCategories()
    {
        var (db, svc) = Build();
        AddItem(db, "R1", "Operasyonel");
        AddItem(db, "R2", "Operasyonel");
        AddItem(db, "R3", "Finansal");
        AddItem(db, "R4", "Arşiv", active: false);

        var cats = svc.GetCategories();

        Assert.Equal(2, cats.Length);
        Assert.Contains("Operasyonel", cats);
        Assert.Contains("Finansal", cats);
        Assert.DoesNotContain("Arşiv", cats);
    }
}
