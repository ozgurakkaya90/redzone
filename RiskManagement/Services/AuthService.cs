using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuthService(AppDbContext db)
{
    public static readonly string[] AllRoles = Roles.All;
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan PasswordResetRequestCooldown = TimeSpan.FromMinutes(2);

    public static readonly Dictionary<string, string> RoleLabels = new()
    {
        [Roles.Admin]        = "Yönetici",
        [Roles.Committee]    = "Risk Komitesi",
        [Roles.RiskManager]  = "Risk Yöneticisi",
        [Roles.RiskOwner]    = "Risk Sahibi",
        [Roles.User]         = "Kullanıcı",
        [Roles.Auditor]      = "Denetçi",
        [Roles.AuditManager] = "Denetim Müdürü",
        [Roles.FindingOwner] = "Bulgu Sahibi",
        [Roles.EthicsBoard]  = "Etik Kurul",
    };

    // Flat permission map (key → label) — used for HasPermission checks
    public static readonly Dictionary<string, string> Permissions = new()
    {
        // Risk
        ["risk.read"]           = "Risk kayıtlarını görüntüle",
        ["risk.write"]          = "Yeni risk öner / oluştur",
        ["risk.modify"]         = "Risk incele, onayla, değerlendir, strateji belirle",
        // Kontrol & Aksiyon
        ["control.read"]        = "Kontrolleri görüntüle",
        ["control.write"]       = "Kontrol ekle",
        ["control.modify"]      = "Kontrol düzenle / sil",
        ["action.read"]         = "Aksiyon planlarını görüntüle",
        ["action.write"]        = "Aksiyon planı oluştur",
        ["action.modify"]       = "Aksiyon durumu güncelle / sil",
        // İç Denetim
        ["audit.read"]          = "Denetimleri ve bulguları görüntüle",
        ["audit.write"]         = "Denetim ve bulgu oluştur",
        ["audit.modify"]        = "Bulgu güncelle / kapanış başvurusu yap",
        ["audit.close_approve"] = "Kapanış başvurularını onayla",
        // Dış Denetim — kurum bazlı yetki UserExternalAuditBodies'te tutulur
        ["external_audit.read"]   = "Dış denetimleri ve bulgularını görüntüle",
        ["external_audit.write"]  = "Dış denetim, bulgu, aksiyon oluştur/güncelle",
        ["external_audit.admin"]  = "Tüm dış denetimleri yönet ve kurum yetkisi ata",
        // Etik
        ["ethics.read"]         = "Etik bildirimleri görüntüle",
        ["ethics.write"]        = "Etik bildirim oluştur",
        ["ethics.audit_review"] = "Denetim değerlendirmesi yap",
        ["ethics.board_review"] = "Kurul değerlendirmesi yap",
    };

    // Grouped structure for the permissions matrix UI
    public static readonly PermGroup[] PermGroups =
    [
        new("Risk Yönetimi", "", "#E1251B", "#fff5f5",
        [
            new("risk.read",   "Okuma",      "Görüntüle"),
            new("risk.write",  "Yazma",      "Öner / Oluştur"),
            new("risk.modify", "Düzenleme",  "İncele, Onayla, Değerlendir"),
        ]),
        new("Kontrol & Aksiyon", "", "#0369a1", "#f0f9ff",
        [
            new("control.read",   "Okuma",     "Kontrolleri görüntüle"),
            new("control.write",  "Yazma",     "Kontrol ekle"),
            new("control.modify", "Düzenleme", "Kontrol düzenle / sil"),
            new("action.read",    "Okuma",     "Aksiyon planlarını görüntüle"),
            new("action.write",   "Yazma",     "Aksiyon planı oluştur"),
            new("action.modify",  "Düzenleme", "Aksiyon güncelle / sil"),
        ]),
        new("İç Denetim", "", "#0f766e", "#f0fdfa",
        [
            new("audit.read",          "Okuma",     "Denetim ve bulguları görüntüle"),
            new("audit.write",         "Yazma",     "Denetim ve bulgu oluştur"),
            new("audit.modify",        "Düzenleme", "Bulgu güncelle / kapanış başvurusu"),
            new("audit.close_approve", "Onaylama",  "Kapanış başvurularını onayla"),
        ]),
        new("Dış Denetim", "", "#b45309", "#fffbeb",
        [
            new("external_audit.read",  "Okuma",      "Dış denetim ve bulguları görüntüle (kurum bazlı)"),
            new("external_audit.write", "Yazma",      "Dış denetim, bulgu, aksiyon oluştur (kurum bazlı)"),
            new("external_audit.admin", "Yönetim",    "Tüm kurumları yönet + kurum yetkisi ata"),
        ]),
        new("Etik Yönetimi", "", "#7c3aed", "#faf5ff",
        [
            new("ethics.read",         "Okuma",       "Etik bildirimleri görüntüle"),
            new("ethics.write",        "Yazma",       "Etik bildirim oluştur"),
            new("ethics.audit_review", "Denetim",     "Denetim değerlendirmesi yap"),
            new("ethics.board_review", "Kurul",       "Kurul değerlendirmesi yap"),
        ]),
    ];

    public static readonly Dictionary<string, string[]> DefaultPermissions = new()
    {
        [Roles.Admin]        = [.. Permissions.Keys],
        [Roles.Committee]    = ["risk.read","risk.modify"],
        [Roles.RiskManager]  = ["risk.read","risk.write","risk.modify","control.read","control.write","control.modify","action.read","action.write","action.modify"],
        [Roles.RiskOwner]    = ["risk.read","risk.write","control.read","control.write","action.read","action.write","action.modify"],
        [Roles.User]         = ["risk.read","risk.write"],
        [Roles.Auditor]      = ["risk.read","audit.read","audit.write","audit.modify","external_audit.read","external_audit.write","ethics.read"],
        [Roles.AuditManager] = ["risk.read","risk.modify","audit.read","audit.write","audit.modify","audit.close_approve","external_audit.read","external_audit.write","external_audit.admin","ethics.read","ethics.audit_review"],
        [Roles.FindingOwner] = ["audit.read","audit.modify"],
        [Roles.EthicsBoard]  = ["ethics.read","ethics.board_review"],
    };

    public async Task<User?> ValidateAsync(string username, string password)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments).ThenInclude(ud => ud.Department)
            .Include(u => u.UserOrganizations).ThenInclude(uo => uo.Organization)
            .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        if (user == null) return null;
        if (user.PasswordHash == "$ldap$") return null; // yalnızca AD girişine izin verilir
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
        return user;
    }

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public static string GenerateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashResetToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    public bool HasPermission(User user, string permission)
    {
        var allRoles = user.AllRoleNames.ToList();
        if (allRoles.Contains("admin")) return true;
        return db.RolePermissions.Any(rp => allRoles.Contains(rp.Role) && rp.Permission == permission);
    }

    public bool HasRole(User? user, params string[] roles)
        => user != null && user.HasAnyRole(roles);

    public ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("full_name", user.FullName),
            new("department", user.Department),
        };

        // Tüm roller — tekrarsız olarak ayrı claim olarak eklenir
        // [Authorize(Roles="admin,committee")] bunları otomatik kontrol eder
        var allRoles = user.AllRoleNames.ToHashSet();
        if (allRoles.Count == 0) allRoles.Add("user");
        foreach (var role in allRoles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Birden fazla departman/org/şirket id'lerini claim olarak sakla
        foreach (var deptId in user.AllDepartmentIds)
            claims.Add(new Claim("dept_id", deptId.ToString()));
        foreach (var orgId in user.AllOrganizationIds)
            claims.Add(new Claim("org_id", orgId.ToString()));
        foreach (var compId in user.AllCompanyIds)
            claims.Add(new Claim("company_id", compId.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "cookie"));
    }

    public static User? UserFromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true) return null;

        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var deptIds = principal.FindAll("dept_id")
            .Select(c => int.TryParse(c.Value, out var v) ? (int?)v : null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var orgIds = principal.FindAll("org_id")
            .Select(c => int.TryParse(c.Value, out var v) ? (int?)v : null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var compIds = principal.FindAll("company_id")
            .Select(c => int.TryParse(c.Value, out var v) ? (int?)v : null)
            .Where(v => v.HasValue).Select(v => v!.Value).ToList();

        return new User
        {
            Id         = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
            Username   = principal.FindFirst(ClaimTypes.Name)?.Value ?? "",
            FullName   = principal.FindFirst("full_name")?.Value ?? "",
            Department = principal.FindFirst("department")?.Value ?? "",
            Role       = roles.FirstOrDefault() ?? "",
            UserRoles  = roles.Select(r => new UserRole { RoleName = r }).ToList(),
            UserDepartments  = deptIds.Select(id => new UserDepartment { DepartmentId = id }).ToList(),
            UserOrganizations = orgIds.Select(id => new UserOrganization { OrganizationId = id }).ToList(),
            UserCompanies    = compIds.Select(id => new UserCompany { CompanyId = id }).ToList(),
        };
    }
}

public record PermGroup(string Name, string Icon, string Color, string Bg, PermItem[] Items);
public record PermItem(string Key, string Level, string Description);
