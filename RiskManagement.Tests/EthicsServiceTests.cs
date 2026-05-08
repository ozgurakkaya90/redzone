using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

// IWebHostEnvironment stub — uploads klasörüne yazmadan servis mantığını test eder
file sealed class StubEnv : IWebHostEnvironment
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    public string WebRootPath      { get => _root; set { } }
    public string ContentRootPath  { get => _root; set { } }
    public string EnvironmentName  { get => "Test"; set { } }
    public string ApplicationName  { get => "Test"; set { } }
    public IFileProvider WebRootFileProvider   { get => null!; set { } }
    public IFileProvider ContentRootFileProvider { get => null!; set { } }
}

public class EthicsServiceTests
{
    private static (AppDbContext db, EthicsService svc) Build()
    {
        var db  = TestDb.Create();
        var svc = new EthicsService(db, new StubEnv());
        return (db, svc);
    }

    // ── Kod üretimi ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_GeneratesUniqueCode()
    {
        var (db, svc) = Build();

        var r1 = await svc.SubmitAsync("Konu 1", "Açıklama 1", null, []);
        var r2 = await svc.SubmitAsync("Konu 2", "Açıklama 2", null, []);

        Assert.NotEqual(r1.Code, r2.Code);
        Assert.StartsWith("EB-", r1.Code);
    }

    [Fact]
    public async Task SubmitAsync_AfterGap_NoConflictWithExisting()
    {
        var (db, svc) = Build();

        var r1 = await svc.SubmitAsync("A", "A", null, []);
        var r2 = await svc.SubmitAsync("B", "B", null, []);
        db.EthicsReports.Remove(r2); db.SaveChanges();
        var r3 = await svc.SubmitAsync("C", "C", null, []);

        // r1 hâlâ mevcut — r3 ile çakışmamalı
        Assert.NotEqual(r1.Code, r3.Code);
        Assert.StartsWith("EB-", r3.Code);
    }

    [Fact]
    public async Task SubmitAsync_StartsAsPending()
    {
        var (db, svc) = Build();
        var report = await svc.SubmitAsync("Test", "Açıklama", "Taciz / Mobbing", []);
        Assert.Equal("pending", report.Status);
    }

    [Fact]
    public async Task SubmitAsync_SetsCategory()
    {
        var (db, svc) = Build();
        var report = await svc.SubmitAsync("Test", "Açıklama", "Mali Usulsüzlük", []);
        Assert.Equal("Mali Usulsüzlük", report.ReportCategory);
    }

    // ── Denetim değerlendirmesi ──────────────────────────────────────────────

    [Fact]
    public async Task AuditReview_Irrelevant_SetsStatusIrrelevant()
    {
        var (db, svc) = Build();
        var reviewer = new User { Username = "mgr", FullName = "Mgr", Role = "audit_manager", Department = "D" };
        db.Users.Add(reviewer); db.SaveChanges();

        var report = await svc.SubmitAsync("Şikayet", "İçerik", null, []);
        svc.AuditReview(report.Id, "irrelevant", "Kapsam dışı", reviewer.Id);

        var updated = db.EthicsReports.Find(report.Id)!;
        Assert.Equal("irrelevant", updated.Status);
        Assert.Equal("irrelevant", updated.AuditDecision);
        Assert.Equal(reviewer.Id, updated.AuditReviewedById);
    }

    [Fact]
    public async Task AuditReview_BoardNotified_AdvancesStatus()
    {
        var (db, svc) = Build();
        var reviewer = new User { Username = "mgr", FullName = "Mgr", Role = "audit_manager", Department = "D" };
        db.Users.Add(reviewer); db.SaveChanges();

        var report = await svc.SubmitAsync("Şikayet", "İçerik", null, []);
        svc.AuditReview(report.Id, "ethics_board_notified", "Kurul devralacak", reviewer.Id);

        Assert.Equal("ethics_board_notified", db.EthicsReports.Find(report.Id)!.Status);
    }

    // ── Etik kurul değerlendirmesi ───────────────────────────────────────────

    [Fact]
    public async Task EthicsReview_NoViolation_ClosesCase()
    {
        var (db, svc) = Build();
        var user = new User { Username = "board", FullName = "Board", Role = "ethics_board", Department = "D" };
        db.Users.Add(user); db.SaveChanges();

        var report = await svc.SubmitAsync("Şikayet", "İçerik", null, []);
        svc.AuditReview(report.Id, "ethics_board_notified", null, user.Id);
        svc.EthicsReview(report.Id, "no_violation", "İncelendi, ihlal yok", user.Id);

        var updated = db.EthicsReports.Find(report.Id)!;
        Assert.Equal("no_violation", updated.Status);
        Assert.Equal("no_violation", updated.EthicsDecision);
    }

    [Fact]
    public async Task EthicsReview_DisciplinaryReferred_SetsCorrectStatus()
    {
        var (db, svc) = Build();
        var user = new User { Username = "board2", FullName = "Board2", Role = "ethics_board", Department = "D" };
        db.Users.Add(user); db.SaveChanges();

        var report = await svc.SubmitAsync("İhlal", "Detay", null, []);
        svc.AuditReview(report.Id, "ethics_board_notified", null, user.Id);
        svc.EthicsReview(report.Id, "disciplinary_referred", "İşleme alındı", user.Id);

        Assert.Equal("disciplinary_referred", db.EthicsReports.Find(report.Id)!.Status);
    }

    // ── Get / GetAll ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ExistingReport_ReturnsReport()
    {
        var (db, svc) = Build();
        var report = await svc.SubmitAsync("Test", "Test", null, []);
        Assert.NotNull(svc.Get(report.Id));
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var (_, svc) = Build();
        Assert.Null(svc.Get(999));
    }

    [Fact]
    public async Task GetAll_StatusFilter_Works()
    {
        var (db, svc) = Build();
        var user = new User { Username = "mgr3", FullName = "Mgr3", Role = "audit_manager", Department = "D" };
        db.Users.Add(user); db.SaveChanges();

        await svc.SubmitAsync("A", "A", null, []);
        var r2 = await svc.SubmitAsync("B", "B", null, []);
        svc.AuditReview(r2.Id, "irrelevant", null, user.Id);

        Assert.Single(svc.GetAll("irrelevant"));
        Assert.Single(svc.GetAll("pending"));
    }
}
