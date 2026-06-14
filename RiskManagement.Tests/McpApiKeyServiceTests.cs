using RiskManagement.Models;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// MCP API anahtarı doğrulamasının güvenlik garantileri: geçerli/yanlış anahtar,
/// iptal sonrası reddi ve kapsam (scope) kullanıcısı deaktive ise anahtarın geçersiz sayılması.
/// </summary>
public class McpApiKeyServiceTests
{
    [Fact]
    public void Validate_CorrectKey_ReturnsMatch_WrongKey_ReturnsNull()
    {
        var db = TestDb.Create();
        var svc = new McpApiKeyService(db);

        var (key, plaintext) = svc.Create("CI anahtarı", createdById: 1);

        Assert.NotNull(svc.Validate(plaintext));
        Assert.Null(svc.Validate("rzk_yanlisanahtar0000"));
        Assert.Null(svc.Validate(""));
        Assert.Null(svc.Validate("baska_prefix_xyz"));
    }

    [Fact]
    public void Validate_RevokedKey_ReturnsNull()
    {
        var db = TestDb.Create();
        var svc = new McpApiKeyService(db);

        var (key, plaintext) = svc.Create("İptal edilecek", createdById: 1);
        Assert.NotNull(svc.Validate(plaintext));

        svc.Revoke(key.Id);
        Assert.Null(svc.Validate(plaintext)); // iptal sonrası geçersiz
    }

    [Fact]
    public void Validate_ScopedKey_DeactivatedUser_ReturnsNull()
    {
        // Gerçek kullanımda her MCP isteği taze bir scoped DbContext alır; testte de
        // paylaşılan store üzerinde her işlem için ayrı context kullanılır.
        var factory = TestDb.CreateFactory();
        int userId;
        string plaintext;

        using (var db = factory.CreateDbContext())
        {
            var user = new User { Username = "scoped", FullName = "Scoped", Role = "auditor", IsActive = true };
            db.Users.Add(user);
            db.SaveChanges();
            userId = user.Id;
        }

        using (var db = factory.CreateDbContext())
        {
            var (key, p) = new McpApiKeyService(db).Create("Kapsamlı anahtar", createdById: 1, scopeUserId: userId);
            Assert.Equal(userId, key.ScopeUserId);
            plaintext = p;
        }

        using (var db = factory.CreateDbContext())
            Assert.NotNull(new McpApiKeyService(db).Validate(plaintext)); // aktif kullanıcı → geçerli

        using (var db = factory.CreateDbContext())
        {
            var u = db.Users.Find(userId)!;
            u.IsActive = false;
            db.SaveChanges();
        }

        using (var db = factory.CreateDbContext())
            Assert.Null(new McpApiKeyService(db).Validate(plaintext)); // deaktive → anahtar geçersiz
    }
}
