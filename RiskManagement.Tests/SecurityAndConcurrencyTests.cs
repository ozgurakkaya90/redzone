using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Data;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

// ── Hedef 3: Kod üretimi eşzamanlılık güvenliği ───────────────────────────────

public class CodeGenerationConcurrencyTests
{
    [Fact]
    public void GenerateAuditCode_SequentialCalls_ProduceUniqueCodesWithCounterPattern()
    {
        var (_, svc) = AuditBuild();
        var c1 = svc.GenerateAuditCode();
        var c2 = svc.GenerateAuditCode();
        var c3 = svc.GenerateAuditCode();

        Assert.NotEqual(c1, c2);
        Assert.NotEqual(c2, c3);
        var year = DateTime.UtcNow.Year;
        Assert.StartsWith($"ID-{year}-", c1);
        Assert.Equal($"ID-{year}-001", c1);
        Assert.Equal($"ID-{year}-002", c2);
        Assert.Equal($"ID-{year}-003", c3);
    }

    [Fact]
    public void GenerateFindingCode_SequentialCalls_ProduceUniqueCodesWithCounterPattern()
    {
        var (_, svc) = AuditBuild();
        var c1 = svc.GenerateFindingCode();
        var c2 = svc.GenerateFindingCode();

        Assert.NotEqual(c1, c2);
        var year = DateTime.UtcNow.Year;
        Assert.Equal($"B-{year}-001", c1);
        Assert.Equal($"B-{year}-002", c2);
    }

    [Fact]
    public void EthicsGenerateCode_SequentialCalls_ProduceUniqueCodesWithCounterPattern()
    {
        var db  = TestDb.Create();
        var env = new TestWebHostEnvironment();
        var svc = new EthicsService(db, env);

        var c1 = svc.GenerateCode();
        var c2 = svc.GenerateCode();

        Assert.NotEqual(c1, c2);
        var year = DateTime.UtcNow.Year;
        Assert.Equal($"EB-{year}-001", c1);
        Assert.Equal($"EB-{year}-002", c2);
    }

    [Fact]
    public void AuditAndFindingCounters_AreIndependent()
    {
        var (_, svc) = AuditBuild();
        var audit1   = svc.GenerateAuditCode();
        var finding1 = svc.GenerateFindingCode();
        var audit2   = svc.GenerateAuditCode();

        var year = DateTime.UtcNow.Year;
        Assert.Equal($"ID-{year}-001", audit1);
        Assert.Equal($"B-{year}-001",  finding1);
        Assert.Equal($"ID-{year}-002", audit2);
    }

    // ConcurrencyCheck: bir bağlam sayacı takip ederken başka bir bağlam
    // aynı satırı güncellediğinde, GetNext taze okuma yaparak doğru değeri döner.
    [Fact]
    public void CounterHelper_ExternalDbUpdate_ReadsCurrentValue()
    {
        var dbPath = TempDbPath();
        try
        {
            using var ctx1 = CreateFileDb(dbPath);
            ctx1.Database.EnsureCreated();

            // İlk değer: 1
            Assert.Equal(1L, CounterHelper.GetNext(ctx1, "ext-test"));

            // Başka bir bağlam value'yu 50'ye çeker (eş zamanlı yazma simülasyonu)
            using (var ctx2 = CreateFileDb(dbPath))
            {
                var c = ctx2.Counters.First(x => x.Key == "ext-test");
                c.Value = 50;
                ctx2.SaveChanges();
            }

            // ctx1 ChangeTracker.Clear() yaparak taze okur → 51 döner
            Assert.Equal(51L, CounterHelper.GetNext(ctx1, "ext-test"));
        }
        finally { CleanupDb(dbPath); }
    }

    // Gerçek paralel erişim: iki bağımsız DbContext, aynı key için eş zamanlı GetNext.
    [Fact]
    public async Task CounterHelper_TwoParallelContexts_ProducesDistinctValues()
    {
        var dbPath = TempDbPath();
        try
        {
            using (var seed = CreateFileDb(dbPath))
                seed.Database.EnsureCreated();

            var t1 = Task.Run(() => { using var ctx = CreateFileDb(dbPath); return CounterHelper.GetNext(ctx, "parallel-2026"); });
            var t2 = Task.Run(() => { using var ctx = CreateFileDb(dbPath); return CounterHelper.GetNext(ctx, "parallel-2026"); });

            var results = await Task.WhenAll(t1, t2);

            Assert.Equal(2, results.Distinct().Count());
            Assert.Contains(1L, results);
            Assert.Contains(2L, results);
        }
        finally { CleanupDb(dbPath); }
    }

    private static string TempDbPath() =>
        Path.Combine(Path.GetTempPath(), $"ct_{Guid.NewGuid():N}.db");

    private static void CleanupDb(string path)
    {
        foreach (var f in new[] { path, path + "-shm", path + "-wal" })
            try { if (File.Exists(f)) File.Delete(f); } catch { /* ignore */ }
    }

    private static AppDbContext CreateFileDb(string path)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;
        return new AppDbContext(opts);
    }

    private static (AppDbContext db, AuditService svc) AuditBuild()
    {
        var db = TestDb.Create();
        return (db, new AuditService(db));
    }
}

// ── Hedef 4: Upload validasyonu ───────────────────────────────────────────────

public class UploadValidationTests
{
    [Theory]
    [InlineData(".pdf",  true)]
    [InlineData(".docx", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".png",  true)]
    [InlineData(".jpg",  true)]
    [InlineData(".zip",  true)]
    [InlineData(".exe",  false)]
    [InlineData(".bat",  false)]
    [InlineData(".sh",   false)]
    [InlineData(".php",  false)]
    [InlineData(".js",   false)]
    [InlineData(".aspx", false)]
    public void AllowedAttachmentExtensions_CorrectlyClassifiesExtensions(string ext, bool expected)
    {
        Assert.Equal(expected, AuditService.AllowedAttachmentExtensions.Contains(ext));
    }

    [Fact]
    public void MaxAttachmentSize_Is10MB()
    {
        Assert.Equal(10L * 1024 * 1024, AuditService.MaxAttachmentSize);
    }

    [Fact]
    public void AllowedAttachmentExtensions_AreAllLowercase()
    {
        foreach (var ext in AuditService.AllowedAttachmentExtensions)
            Assert.Equal(ext, ext.ToLowerInvariant());
    }

    [Fact]
    public async Task SaveAttachment_StoresOriginalFileName_SeparateFromPath()
    {
        var db  = TestDb.Create();
        var svc = new AuditService(db);

        var user = new RiskManagement.Models.User
            { Username = "u", FullName = "U", Role = "auditor" };
        db.Users.Add(user); db.SaveChanges();

        var finding = await svc.CreateFindingAsync("Bulgu", null, null, null, null, user.Id, null, null, null);

        // Stored path Guid içeren farklı bir isim; FileName orijinal
        var stored   = $"{System.Guid.NewGuid()}.pdf";
        var original = "rapor.pdf";
        await svc.SaveAttachmentAsync(finding.Id, original, $"/uploads/{stored}", 1024, user.Id);

        var att = db.FindingAttachments.First(a => a.FindingId == finding.Id);
        Assert.Equal(original, att.FileName);
        Assert.Contains(stored, att.StoredPath);
        Assert.NotEqual(att.FileName, System.IO.Path.GetFileName(att.StoredPath));
    }

    [Fact]
    public void StoredFilenameGuid_IsUnique_AcrossMultipleUploads()
    {
        var storedNames = Enumerable.Range(0, 20)
            .Select(_ => $"{System.Guid.NewGuid()}.pdf")
            .ToHashSet();

        // 20 Guid üretildiyse hepsi farklı olmalı
        Assert.Equal(20, storedNames.Count);
    }

    [Fact]
    public void PathTraversal_IsBlockedByResolveAttachmentPath()
    {
        var att = new RiskManagement.Models.FindingAttachment
        {
            FileName   = "evil.pdf",
            StoredPath = "/etc/passwd",
            FileSize   = 0,
        };

        var result = AuditService.ResolveAttachmentPath(att, "/app", "/app/wwwroot");
        Assert.Null(result);
    }

    [Fact]
    public void PathTraversal_IsBlockedByResolveClosureFilePath()
    {
        var result = AuditService.ResolveClosureFilePath(
            1, "../../etc/passwd", "/app", "/app/wwwroot");
        Assert.Null(result);
    }

    [Fact]
    public void ResolveClosureFilePath_RejectsFilenameWithDirectorySeparator()
    {
        var result = AuditService.ResolveClosureFilePath(
            1, "subdir/evil.pdf", "/app", "/app/wwwroot");
        Assert.Null(result);
    }
}

// ── Hedef 1: Login open redirect ──────────────────────────────────────────────

public class LdapFilterEscapeTests
{
    [Theory]
    [InlineData("normal",           "normal")]
    [InlineData("user*name",        @"user\2aname")]
    [InlineData("(admin)",          @"\28admin\29")]
    [InlineData("a\\b",             @"a\5cb")]
    [InlineData("null\0char",       @"null\00char")]
    [InlineData("ok@domain.com",    "ok@domain.com")]
    public void EscapeLdapFilterValue_EscapesSpecialChars(string input, string expected)
    {
        // EscapeLdapFilterValue is private — tested indirectly via known outputs
        // Here we verify the escape table contract through known good/bad chars.
        var method = typeof(RiskManagement.Pages.LoginModel)
            .GetMethod("EscapeLdapFilterValue",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
        var result = (string)method!.Invoke(null, [input])!;
        Assert.Equal(expected, result);
    }
}

// ── Test yardımcısı ──────────────────────────────────────────────────────────

internal sealed class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string WebRootPath { get; set; } = System.IO.Path.GetTempPath();
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; }
        = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string ApplicationName { get; set; } = "Test";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
        = new Microsoft.Extensions.FileProviders.NullFileProvider();
    public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
    public string EnvironmentName { get; set; } = "Test";
}
