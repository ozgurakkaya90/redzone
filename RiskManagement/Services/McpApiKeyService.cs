using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public record McpApiKeyListItem(
    int Id, string Name, string KeyPrefix,
    bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt,
    string CreatedByName,
    int? ScopeUserId, string? ScopeUserName, string? ScopeUserRole);

public record UserChoice(int Id, string FullName, string Role);

public class McpApiKeyService(AppDbContext db)
{
    public (McpApiKey key, string plaintext) Create(string name, int createdById, int? scopeUserId = null)
    {
        var raw = $"rzk_{GenerateSecret(32)}";
        var key = new McpApiKey
        {
            Name = name,
            KeyPrefix = raw[..8],
            KeyHash = BCrypt.Net.BCrypt.HashPassword(raw),
            CreatedById = createdById,
            ScopeUserId = scopeUserId,
        };
        db.McpApiKeys.Add(key);

        try
        {
            db.SaveChanges();
        }
        catch (Exception ex) when (
            ex.InnerException?.Message.Contains("ScopeUserId") == true ||
            ex.InnerException?.InnerException?.Message.Contains("ScopeUserId") == true)
        {
            // ScopeUserId kolonu henüz yoksa (migration bekleniyor) onsuz kaydet
            db.Entry(key).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            key = new McpApiKey
            {
                Name = name,
                KeyPrefix = raw[..8],
                KeyHash = BCrypt.Net.BCrypt.HashPassword(raw),
                CreatedById = createdById,
                ScopeUserId = null,
            };
            db.McpApiKeys.Add(key);
            db.SaveChanges();
        }

        return (key, raw);
    }

    // Validate: sadece gerekli User kolonlarını seç (Id, FullName, Role)
    public McpApiKey? Validate(string rawKey)
    {
        if (string.IsNullOrEmpty(rawKey) || !rawKey.StartsWith("rzk_"))
            return null;

        var prefix = rawKey[..8];
        var candidates = db.McpApiKeys
            .Where(k => k.KeyPrefix == prefix && k.IsActive)
            .ToList();

        var match = candidates.FirstOrDefault(k => BCrypt.Net.BCrypt.Verify(rawKey, k.KeyHash));
        if (match is null) return null;

        // Scoped key: tam kullanıcıyı yükle; deaktive kullanıcı varsa key geçersiz
        if (match.ScopeUserId.HasValue)
        {
            match.ScopeUser = db.Users
                .Include(u => u.UserRoles)
                .Include(u => u.UserDepartments)
                .Include(u => u.UserOrganizations)
                .Include(u => u.UserCompanies)
                .Where(u => u.Id == match.ScopeUserId.Value && u.IsActive)
                .AsNoTracking()
                .FirstOrDefault();
            if (match.ScopeUser is null) return null; // kullanıcı deaktive
        }

        match.LastUsedAt = DateTime.UtcNow;
        db.SaveChanges();
        return match;
    }

    // Admin UI için: sadece ekranda gösterilecek alanları döndür, tüm User kolonlarını yükleme
    public List<McpApiKeyListItem> GetAll()
    {
        // ScopeUserId kolonu migration henüz çalışmamışsa sütun olmayabilir; graceful fallback
        List<(int Id, string Name, string KeyPrefix, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt, int CreatedById, int? ScopeUserId)> keys;
        try
        {
            keys = db.McpApiKeys
                .AsNoTracking()
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new { k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedAt, k.LastUsedAt, k.CreatedById, k.ScopeUserId })
                .AsEnumerable()
                .Select(k => (k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedAt, k.LastUsedAt, k.CreatedById, k.ScopeUserId))
                .ToList();
        }
        catch
        {
            // ScopeUserId kolonu henüz yoksa onsuz sorgula
            keys = db.McpApiKeys
                .AsNoTracking()
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new { k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedAt, k.LastUsedAt, k.CreatedById })
                .AsEnumerable()
                .Select(k => (k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedAt, k.LastUsedAt, k.CreatedById, (int?)null))
                .ToList();
        }

        var userIds = keys.Select(k => k.CreatedById)
            .Concat(keys.Where(k => k.ScopeUserId.HasValue).Select(k => k.ScopeUserId!.Value))
            .Distinct().ToList();

        var users = db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Role })
            .ToDictionary(u => u.Id);

        return keys.Select(k => new McpApiKeyListItem(
            k.Id, k.Name, k.KeyPrefix, k.IsActive, k.CreatedAt, k.LastUsedAt,
            users.TryGetValue(k.CreatedById, out var cb) ? cb.FullName : "?",
            k.ScopeUserId,
            k.ScopeUserId.HasValue && users.TryGetValue(k.ScopeUserId.Value, out var su) ? su.FullName : null,
            k.ScopeUserId.HasValue && users.TryGetValue(k.ScopeUserId.Value, out var su2) ? su2.Role : null
        )).ToList();
    }

    // Kullanıcı seçim listesi — sadece Id, FullName, Role
    public List<UserChoice> GetUserChoices() =>
        db.Users
            .AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new UserChoice(u.Id, u.FullName, u.Role))
            .ToList();

    public void Revoke(int id)
    {
        var key = db.McpApiKeys.Find(id);
        if (key is null) return;
        key.IsActive = false;
        db.SaveChanges();
    }

    public void Delete(int id)
    {
        var key = db.McpApiKeys.Find(id);
        if (key is null) return;
        db.McpApiKeys.Remove(key);
        db.SaveChanges();
    }

    private static string GenerateSecret(int length)
    {
        var bytes = new byte[length / 2];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLower();
    }
}
