using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using RiskManagement.Data;
using RiskManagement.Mcp;
using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// MCP kapsam (scope) izolasyonu: kapsam-lı bir API anahtarı yalnızca bağlı olduğu
/// kullanıcının erişebildiği riskleri görmeli; tam-erişim anahtarı hepsini görmeli.
/// Bu, scoped MCP anahtarları için en kritik güvenlik sınırıdır.
/// </summary>
public class McpScopeIsolationTests
{
    private sealed class StubEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
    }

    private static RiskManagementTools BuildTools(AppDbContext db, McpRequestContext ctx)
    {
        var cfg  = TestDb.CreateConfigService(db);
        var calc = new RiskCalculator(cfg);
        var auth = new AuthService(db, new MemoryCache(new MemoryCacheOptions()));
        var riskSvc  = new RiskService(db, calc, auth, config: cfg);
        var auditSvc = new AuditService(db);
        var ethics   = new EthicsService(db, new StubEnv());
        var extSvc   = new ExternalAuditService(db, auth);
        return new RiskManagementTools(db, riskSvc, auditSvc, ethics, extSvc, auth, ctx);
    }

    [Fact]
    public void ScopedKey_SeesOnlyOwnRisks_FullAccessSeesAll()
    {
        var db = TestDb.Create();

        // Düşük yetkili kullanıcı (risk.manage YOK → yalnızca önerdiği/sahibi olduğu riskler).
        var u1 = new User { Username = "u1", FullName = "U1", Role = "user" };
        db.Users.Add(u1);
        db.SaveChanges();

        db.Risks.AddRange(
            new Risk { Code = "R-MINE-001",  Title = "Kendi riskim",   Status = "proposed", ProposedById = u1.Id },
            new Risk { Code = "R-OTHER-002", Title = "Başkasının riski", Status = "proposed", ProposedById = 9999 });
        db.SaveChanges();

        // Kapsam-lı: ScopeUser = u1
        var scopedCtx = new McpRequestContext { ScopeUser = new User { Id = u1.Id, Username = "u1", Role = "user" } };
        var scopedOut = BuildTools(db, scopedCtx).ListRisks();

        Assert.Contains("R-MINE-001", scopedOut);
        Assert.DoesNotContain("R-OTHER-002", scopedOut); // kapsam dışı risk sızmamalı

        // Tam erişim: ScopeUser = null
        var fullCtx = new McpRequestContext { ScopeUser = null };
        var fullOut = BuildTools(db, fullCtx).ListRisks();

        Assert.Contains("R-MINE-001", fullOut);
        Assert.Contains("R-OTHER-002", fullOut);
    }
}
