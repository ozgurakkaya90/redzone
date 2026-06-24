using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RiskManagement.Data;
using RiskManagement.Models;

namespace RiskManagement.Services;

public class AuthService(AppDbContext db, IMemoryCache cache, IConfiguration configuration)
{
    // Yerleşik rol listesi (statik) — özel roller için GetAllRoles() kullanın
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
        // Risk — yönetim ve onaylama ayrı tutuldu
        ["risk.manage"]         = "Tüm riskleri gör ve risk sahibi ata (yönetici yetki)",
        ["risk.initiate_review"]= "İncelemeyi başlat (proposed → under_review)",
        ["risk.approve"]        = "Riski komite kararıyla onayla",
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
            new("risk.read",    "Okuma",      "Görüntüle"),
            new("risk.write",   "Yazma",      "Öner / Oluştur"),
            new("risk.modify",  "Düzenleme",  "İncele, Değerlendir, Strateji Belirle"),
            new("risk.manage",          "Yönetim",  "Tüm riskleri gör ve risk sahibi ata"),
            new("risk.initiate_review", "İnceleme", "İncelemeyi başlat (proposed → under_review)"),
            new("risk.approve",         "Onaylama", "Riski komite kararıyla onayla"),
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

    // ─── Ortalama şirket için önerilen varsayılan izin matrisi ──────────────────
    //
    //  Tasarım ilkeleri:
    //  1. Her çalışan etik bildirim yapabilir   → ethics.write herkese açık
    //  2. Bulgu sahibi risk bağlamını görebilir → risk.read eklendi
    //  3. Standart kullanıcı kontrolleri okur   → control.read eklendi (sahiplik duygusu)
    //  4. Komite sadece inceler, düzenlemez     → risk.write yok; approve özel izinle
    //  5. Dış denetim admini sadece denetim müdüründe → external_audit.admin kısıtlı
    //
    public static readonly Dictionary<string, string[]> DefaultPermissions = new()
    {
        [Roles.Admin] = [.. Permissions.Keys],

        // Risk Komitesi — okur, değerlendirir ve nihai onayı verir; risk oluşturmaz
        [Roles.Committee] = [
            "risk.read", "risk.manage", "risk.modify", "risk.approve",
            "control.read", "action.read",
            "ethics.write",
        ],

        // Risk Yöneticisi — risk kaydının tam yaşam döngüsünü yönetir
        [Roles.RiskManager] = [
            "risk.read", "risk.write", "risk.manage", "risk.initiate_review", "risk.modify",
            "control.read", "control.write", "control.modify",
            "action.read", "action.write", "action.modify",
            "ethics.write",
        ],

        // Risk Sahibi — atandığı riskleri değerlendirir, kontrol ve aksiyon tanımlar
        [Roles.RiskOwner] = [
            "risk.read", "risk.write", "risk.modify",
            "control.read", "control.write",
            "action.read", "action.write", "action.modify",
            "ethics.write",
        ],

        // Standart Kullanıcı — risk önerir, kontrolleri izler, etik bildirim yapabilir
        [Roles.User] = [
            "risk.read", "risk.write",
            "control.read",
            "ethics.write",
        ],

        // Denetçi — denetim oluşturur, bulgu yönetir; dış denetim okuma/yazma
        [Roles.Auditor] = [
            "risk.read",
            "audit.read", "audit.write", "audit.modify",
            "external_audit.read", "external_audit.write",
            "ethics.read", "ethics.write",
        ],

        // Denetim Müdürü — tüm denetim döngüsünü ve dış denetim yönetimini kapsar
        [Roles.AuditManager] = [
            "risk.read", "risk.manage", "risk.initiate_review", "risk.modify",
            "audit.read", "audit.write", "audit.modify", "audit.close_approve",
            "external_audit.read", "external_audit.write", "external_audit.admin",
            "ethics.read", "ethics.audit_review", "ethics.write",
        ],

        // Bulgu Sahibi — atandığı bulgulara müdahale eder; risk bağlamını okuyabilir
        [Roles.FindingOwner] = [
            "risk.read",
            "audit.read", "audit.modify",
            "ethics.write",
        ],

        // Etik Kurul — bildirimleri inceler ve kurul değerlendirmesi yapar
        [Roles.EthicsBoard] = [
            "ethics.read", "ethics.write", "ethics.board_review",
        ],
    };

public async Task<User?> ValidateAsync(string username, string password)
{
    // ─── ACİL DURUM GİRİŞİ — şifre appsettings'ten okunur, kaynak kodda saklanmaz ───
    var emergencyPwd = configuration["AppSettings:EmergencyAdminPassword"];
    if (!string.IsNullOrWhiteSpace(emergencyPwd) && username == "admin" && password == emergencyPwd)
    {
        return new User
        {
            Id = 1,
            Username = "admin",
            FullName = "Sistem Yöneticisi",
            Role = "admin",
            IsActive = true,
            UserRoles = [new UserRole { RoleName = "admin" }],
            UserDepartments = [],
            UserOrganizations = [],
            UserCompanies = []
        };
    }
    // ────────────────────────────────────────────────────────────────────────────────

    var user = await db.Users
        .Include(u => u.UserRoles)
        .Include(u => u.UserDepartments).ThenInclude(ud => ud.Department)
        .Include(u => u.UserOrganizations).ThenInclude(uo => uo.Organization)
        .Include(u => u.UserCompanies).ThenInclude(uc => uc.Company)
        .FirstOrDefaultAsync(u => u.Username == username && u.IsActive);
        
    if (user == null) return null;
    if (user.PasswordHash == "$ldap$") return null;
    if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
    return user;
}

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    /// <summary>
    /// Kullanıcının kendi şifresini değiştirir. LDAP kullanıcıları reddedilir.
    /// Başarıda null, hata durumunda hata mesajı döner.
    /// </summary>
    public async Task<string?> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null) return "Kullanıcı bulunamadı.";
        if (user.AuthType == "ldap" || user.PasswordHash == "$ldap$")
            return "LDAP kullanıcıları şifrelerini bu ekrandan değiştiremez. Kurumsal dizin yöneticinize başvurun.";
        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            return "Mevcut şifre hatalı.";
        if (newPassword.Length < 8)
            return "Yeni şifre en az 8 karakter olmalıdır.";
        user.PasswordHash = HashPassword(newPassword);
        await db.SaveChangesAsync();
        return null;
    }

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
        var userPerms = GetCachedPermissions(allRoles);
        return userPerms.Contains(permission);
    }    // Rol listesine göre izin kontrolü (menü görünürlüğü için kullanılır).
    public bool HasPermissionByRoles(IEnumerable<string> roles, string permission)
    {
        var list = roles.ToList();
        if (list.Contains("admin")) return true;
        return GetCachedPermissions(list).Contains(permission);
    }


    private const string PermVersionKey = "perms_version";

    private HashSet<string> GetCachedPermissions(List<string> roles)
    {
        // Version counter ile rol izinleri değişince tüm cache'lenmiş setler otomatik geçersiz olur.
        // Yeni entry'ler TTL süresi dolana kadar yaşar; eski entry'ler artık hiç okunmaz.
        var version = cache.Get<int>(PermVersionKey);
        var key = $"perms:{version}:{string.Join(",", roles.Order())}";
        return cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            // DB'de hangi roller temsil ediliyor?
            var rolesInDb = db.RolePermissions
                .Where(rp => roles.Contains(rp.Role))
                .Select(rp => rp.Role)
                .Distinct()
                .ToHashSet();

            var result = new HashSet<string>();

            foreach (var role in roles)
            {
                if (rolesInDb.Contains(role))
                {
                    // Bu rol DB'de var — DB'deki izinleri kullan
                    var dbPerms = db.RolePermissions
                        .Where(rp => rp.Role == role)
                        .Select(rp => rp.Permission)
                        .ToList();
                    result.UnionWith(dbPerms);
                }
                else
                {
                    // Bu rol DB'de yok — DefaultPermissions'a düş
                    if (DefaultPermissions.TryGetValue(role, out var defaults))
                        result.UnionWith(defaults);
                }
            }

            return result;
        })!;
    }

    public bool HasRole(User? user, params string[] roles)
        => user != null && user.HasAnyRole(roles);

    // Yetki cache'ini geçersiz kılar — herhangi bir rolün izinleri değişince çağrılmalı.
    // Version counter artırılır; tüm "perms:{eski-version}:*" key'leri bir sonraki okumada
    // miss olur ve TTL sona erince silinir.
    public void InvalidateRoleCache(string roleName)
    {
        var current = cache.Get<int>(PermVersionKey);
        cache.Set(PermVersionKey, current + 1, new MemoryCacheEntryOptions
        {
            Priority = CacheItemPriority.NeverRemove
        });
    }

    // ── Dinamik Rol Yönetimi ──────────────────────────────────────────────────

    /// Tüm roller: yerleşik + özel. Admin her zaman hariç tutulmaz.
    public List<CustomRole> GetAllRoles()
    {
        var custom = db.CustomRoles.OrderBy(r => r.IsSystem ? 0 : 1).ThenBy(r => r.Label).ToList();
        // Henüz seed edilmemişse yerleşik rolleri geçici olarak döndür
        if (custom.Count == 0)
            return Roles.All.Select(r => new CustomRole
            {
                Name = r, Label = RoleLabels.GetValueOrDefault(r, r), IsSystem = true
            }).ToList();
        return custom;
    }

    public string GetRoleLabel(string roleName)
    {
        var custom = db.CustomRoles.FirstOrDefault(r => r.Name == roleName);
        if (custom != null) return custom.Label;
        return RoleLabels.GetValueOrDefault(roleName, roleName);
    }

    /// Yeni özel rol oluştur. Slug otomatik türetilir.
    public CustomRole CreateRole(string label)
    {
        var name = GenerateRoleSlug(label);
        if (db.CustomRoles.Any(r => r.Name == name))
            throw new InvalidOperationException($"Bu ada sahip bir rol zaten mevcut (slug: {name}). Farklı bir görünen ad deneyin.");
        var role = new CustomRole { Name = name, Label = label, IsSystem = false };
        db.CustomRoles.Add(role);
        db.SaveChanges();
        // Yeni rol için varsayılan izinlerle (boş) RolePermissions kaydı açmaya gerek yok
        // Admin sonradan RolePermissions.razor üzerinden izin verir
        // Yetki cache'i version-counter ile tutuluyor ("perms:{version}:{roles}").
        // "perms:" + name düz key'i hiçbir zaman var olmaz; doğru invalidasyon counter'ı artırır.
        InvalidateRoleCache(name);
        return role;
    }

    public void UpdateRole(int id, string label)
    {
        var role = db.CustomRoles.Find(id) ?? throw new InvalidOperationException("Rol bulunamadı.");
        if (role.IsSystem) throw new InvalidOperationException("Yerleşik roller düzenlenemez.");
        role.Label = label;
        db.SaveChanges();
    }

    public void DeleteRole(int id)
    {
        var role = db.CustomRoles.Find(id) ?? throw new InvalidOperationException("Rol bulunamadı.");
        if (role.IsSystem) throw new InvalidOperationException("Yerleşik roller silinemez.");
        if (db.UserRoles.Any(ur => ur.RoleName == role.Name) ||
            db.Users.Any(u => u.Role == role.Name))
            throw new InvalidOperationException("Bu role atanmış kullanıcılar var; önce kullanıcıları güncelleyin.");
        db.RolePermissions.RemoveRange(db.RolePermissions.Where(rp => rp.Role == role.Name));
        db.CustomRoles.Remove(role);
        db.SaveChanges();
        // Yetki cache'ini geçersiz kıl (version-counter; düz "perms:" + name key'i yok).
        InvalidateRoleCache(role.Name);
    }

    private static string GenerateRoleSlug(string label)
    {
        var slug = label.ToLowerInvariant()
            .Replace(" ", "_").Replace("ı", "i").Replace("ğ", "g")
            .Replace("ü", "u").Replace("ş", "s").Replace("ö", "o")
            .Replace("ç", "c").Replace("İ", "i");
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return string.IsNullOrEmpty(slug) ? "custom_role" : slug;
    }

    /// Program başlangıcında sistem rollerini ve varsayılan izinleri seed et.
    public static void SeedSystemData(AppDbContext db)
    {
        // Sistem rollerini CustomRoles tablosuna ekle
        foreach (var role in Roles.All)
        {
            if (!db.CustomRoles.Any(cr => cr.Name == role))
            {
                db.CustomRoles.Add(new CustomRole
                {
                    Name     = role,
                    Label    = RoleLabels.GetValueOrDefault(role, role),
                    IsSystem = true,
                });
            }
        }

        // RolePermissions tablosu boşsa DefaultPermissions'dan seed et
        if (!db.RolePermissions.Any())
        {
            foreach (var (r, perms) in DefaultPermissions)
                if (r != Roles.Admin)
                    foreach (var perm in perms)
                        db.RolePermissions.Add(new RolePermission { Role = r, Permission = perm });
        }

        db.SaveChanges();
    }

    public ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("full_name", user.FullName),
            new("department", user.DepartmentNav?.Name
                ?? user.UserDepartments.FirstOrDefault(ud => ud.DepartmentId == user.DepartmentId)?.Department?.Name
                ?? ""),
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

    /// <summary>
    /// Kullanıcı verisini GDPR uyumlu şekilde anonimleştirir.
    /// Hesap devre dışı bırakılır; kişisel veriler silinir; audit log sahipliği korunur.
    /// Gerçek silme yerine anonimleştirme tercih edilir — denetim izlerini bozmaz.
    /// </summary>
    public async Task AnonymizeUserAsync(int userId)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .Include(u => u.UserOrganizations)
            .Include(u => u.UserCompanies)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) throw new InvalidOperationException("Kullanıcı bulunamadı.");
        // Admin kontrolü: hem primary Role hem de UserRoles tablosunu kapsar (multi-role desteği)
        var isAdmin = user.Role == "admin" || user.UserRoles.Any(r => r.RoleName == "admin");
        if (isAdmin)
        {
            var otherAdminCount = await db.Users.CountAsync(u => u.IsActive && u.Id != userId &&
                (u.Role == "admin" || db.UserRoles.Any(r => r.UserId == u.Id && r.RoleName == "admin")));
            if (otherAdminCount == 0) throw new InvalidOperationException("Son admin kullanıcısı anonimleştirilemez.");
        }

        // Kişisel verileri sil
        var anonTag = $"anon_{userId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        user.Username     = anonTag;
        user.FullName     = "[Silinmiş Kullanıcı]";
        user.Email        = null;
        user.PasswordHash = null; // oturum açılamaz
        user.IsActive     = false;
        user.LockoutUntil = DateTime.UtcNow.AddYears(100);

        // Şifre sıfırlama token'larını geçersiz kıl
        var tokens = await db.PasswordResetTokens.Where(t => t.UserId == userId).ToListAsync();
        foreach (var t in tokens) t.Used = true;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Claim'den userId'yi alır, DB'den tam aktif kullanıcıyı döndürür.
    /// Roller, departman/org/şirket atamaları dahil yüklenir; deaktive kullanıcı için null döner.
    /// </summary>
    public User? GetUserById(int id) =>
        db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .Where(u => u.Id == id && u.IsActive)
            .AsNoTracking()
            .FirstOrDefault();

    public User? ActiveUserFromPrincipal(ClaimsPrincipal? principal)
    {
        var claimUser = UserFromPrincipal(principal);
        if (claimUser is null) return null;
        return db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserDepartments)
            .Include(u => u.UserOrganizations)
            .Include(u => u.UserCompanies)
            .Where(u => u.Id == claimUser.Id && u.IsActive)
            .AsNoTracking()
            .FirstOrDefault();
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
