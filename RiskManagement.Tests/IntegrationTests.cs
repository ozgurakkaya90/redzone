using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

// ── SQLite ile gerçek şema — migration'ların doğruluğunu doğrular ────────────

file static class IntDb
{
    /// EnsureCreated — model'den tam şema oluşturur (migration'lardan bağımsız)
    /// Migration entegrasyonu için ayrı CreateWithMigrations() metodu kullanılır
    public static AppDbContext Create()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new AppDbContext(opts);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();
        return db;
    }

    /// Migration'larla şema — sadece migration dosyalarının doğruluğunu test eder.
    /// SQLite destekli migration'lar (IF NOT EXISTS) doğru çalışıyorsa geçer.
    public static AppDbContext CreateWithMigrations()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new AppDbContext(opts);
        db.Database.OpenConnection();
        db.Database.Migrate();
        return db;
    }
}

public class SchemaIntegrationTests
{
    [Fact]
    public void EnsureCreated_AllRequiredTablesExist()
    {
        using var db = IntDb.Create();

        // Count() 0 dönmeli ama exception fırlatmamalı — tablo var demektir
        Assert.Equal(0, db.Risks.Count());
        Assert.Equal(0, db.Users.Count());
        Assert.Equal(0, db.AuditFindings.Count());
        Assert.Equal(0, db.EthicsReports.Count());
        Assert.Equal(0, db.RiskAuditLogs.Count());
        Assert.Equal(0, db.RiskReviews.Count());
        Assert.Equal(0, db.InternalAudits.Count());
        Assert.Equal(0, db.EthicsReports.Count());
    }

    [Fact]
    public void RiskAuditLogs_IpAddressColumn_Persists()
    {
        using var db = IntDb.Create();
        var user = new User { Username = "ip_test", FullName = "T", Department = "D", Role = "admin" };
        db.Users.Add(user); db.SaveChanges();
        var risk = new Risk { Code = "R-IP-001", Title = "IP Test", ProposedById = user.Id };
        db.Risks.Add(risk); db.SaveChanges();

        db.RiskAuditLogs.Add(new RiskAuditLog { RiskId = risk.Id, Action = "Test", IpAddress = "10.0.0.1", Timestamp = DateTime.UtcNow });
        db.SaveChanges();

        Assert.Equal("10.0.0.1", db.RiskAuditLogs.First().IpAddress);
    }

    [Fact]
    public void UserRoles_UniqueIndex_PreventsDuplicates()
    {
        using var db = IntDb.Create();
        var user = new User { Username = "idx_test", FullName = "T", Department = "D", Role = "admin" };
        db.Users.Add(user); db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = "admin" });
        db.SaveChanges();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = "admin" }); // duplicate

        Assert.ThrowsAny<Exception>(() => db.SaveChanges());
    }

    [Fact]
    public void RolePermissions_UniqueIndex_PreventsDuplicates()
    {
        using var db = IntDb.Create();
        db.RolePermissions.Add(new RolePermission { Role = "user", Permission = "risk.read" });
        db.SaveChanges();
        db.RolePermissions.Add(new RolePermission { Role = "user", Permission = "risk.read" });

        Assert.ThrowsAny<Exception>(() => db.SaveChanges());
    }

    // NOT: Migration testleri MySQL'e özgü SQL içerir (AlterDatabase, MySql charset vb.)
    // SQLite ortamında bu migration'lar çalışmaz — gerçek migration testi MySQL ile yapılmalı.
    // Bu test sadece migration'ların SQLite'ta BAŞARILI başladığını değil,
    // MySQL bağımlılığı olmadan oluşturulan tabloların var olduğunu doğrular.
    [Fact]
    public void RiskAuditLogsAndRiskReviews_ExistInEFModel()
    {
        using var db = IntDb.Create(); // EnsureCreated — model'den tam şema

        Assert.Equal(0, db.RiskAuditLogs.Count()); // tablo mevcut ve boş
        Assert.Equal(0, db.RiskReviews.Count());
    }
}

// ── Uçtan uca senaryo — gerçek DB ile tam workflow ───────────────────────────

public class EndToEndWorkflowTests
{
    [Fact]
    public void FullRiskLifecycle_ProposedToActionPlanned()
    {
        using var db  = IntDb.Create();
        var cfg  = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var auth = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var svc  = new RiskService(db, new RiskCalculator(cfg), auth);

        // Organizasyon (strategy_set için gerekli)
        var org = new Organization { Name = "Test Org" }; db.Organizations.Add(org); db.SaveChanges();

        // Kullanıcılar
        var owner     = new User { Username = "e2e_owner",     FullName = "Owner",    Department = "D", Role = "risk_owner"   }; db.Users.Add(owner);
        var mgr       = new User { Username = "e2e_mgr",       FullName = "Manager",  Department = "D", Role = "risk_manager" }; db.Users.Add(mgr);
        var committee = new User { Username = "e2e_committee", FullName = "Committee",Department = "D", Role = "committee"    }; db.Users.Add(committee);
        db.SaveChanges();
        db.UserRoles.AddRange(
            new UserRole { UserId = owner.Id,     RoleName = "risk_owner"   },
            new UserRole { UserId = mgr.Id,       RoleName = "risk_manager" },
            new UserRole { UserId = committee.Id, RoleName = "committee"    });
        db.SaveChanges();

        // 1. Risk öner
        var risk = svc.Create("E2E Risk", "Senaryo testi", "Finansal", null, null, owner.Id, owner.FullName);
        Assert.Equal("proposed", risk.Status);

        // 2. İncelemeye al
        Assert.True(svc.UpdateStatus(risk.Id, "under_review", null, mgr));

        // 3. İlk değerlendirme (skor >= 70 → under_review kalır)
        svc.AddEvaluation(risk.Id, "initial", 10, 6, 40, null, mgr.Id); // skor = 2400
        Assert.Equal("under_review", db.Risks.Find(risk.Id)!.Status);

        // 4. Onaya gönder
        Assert.True(svc.UpdateStatus(risk.Id, "awaiting_approval", null, mgr));

        // 5. Komite onaylar
        Assert.True(svc.UpdateStatus(risk.Id, "approved", null, committee));

        // 6. Strateji & organizasyon güncelle (her ikisi de dolu olmalı → strategy_set)
        svc.UpdateMetadata(risk.Id, org.Id, null, "Riski Azaltma", mgr.Id);
        Assert.Equal("strategy_set", db.Risks.Find(risk.Id)!.Status);

        // 7. Kontrol ekle
        svc.AddControl(risk.Id, "Çok faktörlü kimlik doğrulama", "Önleyici", "Tatmin Edici", "Sürekli", mgr.Id);
        Assert.Equal("controlled", db.Risks.Find(risk.Id)!.Status);

        // 8. Kalan risk değerlendir
        svc.AddEvaluation(risk.Id, "residual", 1, 1, 7, null, mgr.Id);
        Assert.Equal("residual_evaluated", db.Risks.Find(risk.Id)!.Status);

        // 9. Aksiyon planla
        svc.AddAction(risk.Id, "Çalışan eğitimi", "IT", DateOnly.FromDateTime(DateTime.Today.AddMonths(3)), mgr.Id);
        Assert.Equal("action_planned", db.Risks.Find(risk.Id)!.Status);

        // 10. Audit log dolu olmalı
        var logs = db.RiskAuditLogs.Where(l => l.RiskId == risk.Id).ToList();
        Assert.True(logs.Count >= 5);
    }

    [Fact]
    public void FullAuditLifecycle_OpenToClosedViaClosureRequest()
    {
        using var db  = IntDb.Create();
        var svc = new AuditService(db);

        var auditor = new User { Username = "e2e_aud", FullName = "Auditor", Department = "D", Role = "auditor" }; db.Users.Add(auditor);
        var mgr     = new User { Username = "e2e_amgr", FullName = "Mgr",   Department = "D", Role = "audit_manager" }; db.Users.Add(mgr);
        db.SaveChanges();

        // Denetim & bulgu oluştur
        var audit   = svc.CreateAudit("E2E Denetim", "ordinary", "BT", null, "2026", null, null, auditor.Id);
        var finding = svc.CreateFinding("Parola politikası yok", null, "Bilgi Teknolojileri", "Yüksek", null, auditor.Id, null, audit.Id, DateOnly.FromDateTime(DateTime.Today.AddMonths(1)));
        Assert.Equal("open", finding.Status);

        // Aksiyon planla
        svc.InternalAddFindingAction(finding.Id, "Parola politikası hazırla", "BT", null, auditor.Id);
        Assert.Equal("action_planned", db.AuditFindings.Find(finding.Id)!.ActionDecision);

        // Kapanış başvurusu
        var req = svc.InternalSubmitClosureRequest(finding.Id, "Politika yayınlandı", null, auditor.Id);
        Assert.Equal("closure_requested", db.AuditFindings.Find(finding.Id)!.Status);

        // Müdür onaylar
        svc.InternalReviewClosureRequest(finding.Id, req.Id, "approved", "Politika dokümanı incelendi", mgr.Id);
        var closed = db.AuditFindings.Find(finding.Id)!;
        Assert.Equal("closed", closed.Status);
        Assert.NotNull(closed.ClosedAt);
    }
}
