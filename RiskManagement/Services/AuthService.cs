using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuthService(AppDbContext db, IConfiguration config)
{
    public static readonly string[] AllRoles =
        ["user","risk_owner","committee","admin","auditor","audit_manager","finding_owner","ethics_board"];

    public static readonly Dictionary<string, string> RoleLabels = new()
    {
        ["admin"]         = "Yönetici",
        ["committee"]     = "Risk Komitesi",
        ["risk_owner"]    = "Risk Sahibi",
        ["user"]          = "Kullanıcı",
        ["auditor"]       = "Denetçi",
        ["audit_manager"] = "Denetim Müdürü",
        ["finding_owner"] = "Bulgu Sahibi",
        ["ethics_board"]  = "Etik Kurul",
    };

    public static readonly Dictionary<string, string> Permissions = new()
    {
        ["risk.propose"]        = "Risk önerme",
        ["risk.review"]         = "Risk inceleme / durum güncelleme",
        ["risk.evaluate"]       = "Risk değerlendirme",
        ["risk.control"]        = "Kontrol ekleme / silme",
        ["risk.action"]         = "Aksiyon planı yönetimi",
        ["audit.create"]        = "Denetim bulgusu oluşturma",
        ["audit.update"]        = "Denetim bulgusu güncelleme",
        ["audit.close_request"] = "Kapanış başvurusu yapma",
        ["audit.close_approve"] = "Kapanış başvurusunu onaylama",
        ["ethics.audit_review"] = "Etik bildirim — denetim değerlendirmesi",
        ["ethics.board_review"] = "Etik bildirim — kurul değerlendirmesi",
    };

    public static readonly Dictionary<string, string[]> DefaultPermissions = new()
    {
        ["admin"]         = [.. Permissions.Keys],
        ["committee"]     = ["risk.review","risk.evaluate","risk.control","risk.action"],
        ["risk_owner"]    = ["risk.propose","risk.evaluate","risk.control","risk.action"],
        ["user"]          = ["risk.propose"],
        ["auditor"]       = ["risk.propose","audit.create","audit.update","audit.close_request"],
        ["audit_manager"] = ["risk.review","audit.create","audit.update","audit.close_request","audit.close_approve","ethics.audit_review"],
        ["finding_owner"] = ["audit.close_request"],
        ["ethics_board"]  = ["ethics.board_review"],
    };

    public async Task<User?> ValidateAsync(string username, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
        return user;
    }

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool HasPermission(User user, string permission)
    {
        if (user.Role == "admin") return true;
        return db.RolePermissions.Any(rp => rp.Role == user.Role && rp.Permission == permission);
    }

    public bool HasRole(User? user, params string[] roles)
        => user != null && roles.Contains(user.Role);

    public ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("full_name", user.FullName),
            new("department", user.Department),
            new(ClaimTypes.Role, user.Role),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
    }

    public static User? UserFromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;
        return new User
        {
            Id = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
            Username = principal.FindFirst(ClaimTypes.Name)?.Value ?? "",
            FullName = principal.FindFirst("full_name")?.Value ?? "",
            Department = principal.FindFirst("department")?.Value ?? "",
            Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? "",
        };
    }
}
