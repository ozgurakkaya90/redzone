using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Novell.Directory.Ldap;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using Microsoft.Extensions.Logging;
using System.Text;

namespace RiskManagement.Pages;

[EnableRateLimiting("login")]
public class LoginModel(AuthService auth, AppDbContext db, ConfigService config, ILogger<LoginModel> logger) : PageModel
{
    public string Error    { get; private set; } = "";
    public string Username { get; private set; } = "";
    public string AuthType { get; private set; } = "local";
    public bool LdapEnabled   { get; private set; }
    public bool EthicsEnabled { get; private set; }

    public void OnGet() => LoadFlags();

    public async Task<IActionResult> OnPostAsync(
        string username, string password, string? returnUrl, string authType = "local")
    {
        LoadFlags();
        Username = username?.Trim() ?? "";
        AuthType = authType == "ad" ? "ad" : "local";

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            Error = "Kullanıcı adı ve şifre boş bırakılamaz.";
            return Page();
        }

        // Hesap kilidi kontrolü (yalnızca lokal giriş; LDAP için AD kendi kilitler)
        // Mesaj kullanıcı varlığını sızdırmamak için geneldir.
        if (AuthType != "ad")
        {
            var maxFails = config.GetMaxFailedLogins();
            if (maxFails > 0)
            {
                var candidate = db.Users.FirstOrDefault(u => u.Username == Username && u.AuthType == "local");
                if (candidate?.LockoutUntil > DateTime.UtcNow)
                {
                    logger.LogWarning("Kilitli hesaba giriş denemesi: {Username}", Username);
                    // Enum: "too many failed attempts" — kullanıcı varlığını açıklamaz
                    Error = "Çok fazla hatalı giriş denemesi yapıldı. Lütfen daha sonra tekrar deneyin.";
                    return Page();
                }
            }
        }

        Models.User? user = null;

        if (AuthType == "ad")
            user = await TryLdapLoginAsync(Username, password);
        else
            user = await auth.ValidateAsync(Username, password);

        if (user == null)
        {
            logger.LogWarning("Hatalı giriş denemesi: {Username}, Tür: {AuthType}", Username, AuthType);

            // Başarısız giriş sayacını güncelle
            if (AuthType != "ad")
            {
                var maxFails = config.GetMaxFailedLogins();
                if (maxFails > 0)
                {
                    var dbUser = db.Users.FirstOrDefault(u => u.Username == Username && u.AuthType == "local");
                    if (dbUser != null)
                    {
                        dbUser.FailedLoginCount++;
                        if (dbUser.FailedLoginCount >= maxFails)
                        {
                            var lockoutMins = config.GetLockoutMinutes();
                            dbUser.LockoutUntil = DateTime.UtcNow.AddMinutes(lockoutMins);
                            dbUser.FailedLoginCount = 0;
                            logger.LogWarning("Hesap kilitlendi: {Username} ({Count} hatalı giriş, {Min} dk)", Username, maxFails, lockoutMins);
                        }
                        db.SaveChanges();
                    }
                }
            }

            Error = AuthType == "ad"
                ? "Active Directory kimlik doğrulaması başarısız. Kullanıcı adı veya şifrenizi kontrol edin."
                : "Kullanıcı adı veya şifre hatalı.";
            return Page();
        }

        // Başarılı giriş: sayacı sıfırla
        if (user.FailedLoginCount > 0 || user.LockoutUntil.HasValue)
        {
            user.FailedLoginCount = 0;
            user.LockoutUntil    = null;
            db.SaveChanges();
        }

        logger.LogInformation("Kullanıcı giriş yaptı: {Username}", user.Username);
        var principal = auth.BuildPrincipal(user);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        // Reverse proxies can produce absolute returnUrls with http:// — extract just the path
        if (!string.IsNullOrEmpty(returnUrl) && Uri.TryCreate(returnUrl, UriKind.Absolute, out var parsed))
            returnUrl = parsed.PathAndQuery;
        var destination = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl : "/dashboard";
        return LocalRedirect(destination);
    }

    private async Task<Models.User?> TryLdapLoginAsync(string username, string password)
    {
        var cfg = db.LdapConfigurations.FirstOrDefault();
        if (cfg == null || !cfg.Enabled)
        {
            Error = "Active Directory entegrasyonu henüz yapılandırılmamış veya devre dışı.";
            return null;
        }

        if (string.IsNullOrWhiteSpace(cfg.Server) || string.IsNullOrWhiteSpace(cfg.UserSearchBase))
        {
            Error = "LDAP sunucu yapılandırması eksik.";
            return null;
        }

        // DOMAIN\user → user
        var samAccount = username.Contains('\\') ? username.Split('\\')[1] : username;
        var escapedAccount = EscapeLdapFilterValue(samAccount);
        var filter = cfg.UserSearchFilter.Replace("{username}", escapedAccount);

        try
        {
            return await Task.Run(() => PerformLdapLogin(cfg, samAccount, password, filter));
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            logger.LogWarning("LDAP kimlik doğrulama başarısız: {User}", samAccount);
            Error = "Active Directory kimlik doğrulaması başarısız.";
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "LDAP bağlantı hatası: {Server}:{Port}", cfg.Server, cfg.Port);
            Error = "Active Directory sunucusuyla iletişim kurulamadı.";
            return null;
        }
    }

    private void LoadFlags()
    {
        LdapEnabled   = db.LdapConfigurations.FirstOrDefault()?.Enabled ?? false;
        EthicsEnabled = config.IsModuleActive("ethics");
    }

    private Models.User? PerformLdapLogin(LdapConfiguration cfg, string samAccount,
        string password, string searchFilter)
    {
        string? userDn      = null;
        string? displayName = null;
        string? email       = null;
        string? department  = null;

        // 1. Servis hesabıyla bağlan ve kullanıcıyı ara
        using (var conn = new LdapConnection { SecureSocketLayer = cfg.UseSsl })
        {
            conn.Connect(cfg.Server!, cfg.Port);
            if (cfg.UseTls) conn.StartTls();

            if (!string.IsNullOrEmpty(cfg.BindDn))
                conn.Bind(cfg.BindDn, cfg.BindPassword ?? "");
            else
                conn.Bind("", ""); // anonim bind

            var attrs = new[] { cfg.AttrDisplayName, cfg.AttrEmail, cfg.AttrDepartment, "dn" };
            var results = conn.Search(cfg.UserSearchBase, LdapConnection.ScopeSub,
                searchFilter, attrs, false);

            if (!results.HasMore())
            {
                Error = "Active Directory'de kullanıcı bulunamadı.";
                return null;
            }

            var entry = results.Next();
            userDn      = entry.Dn;
            displayName = GetAttr(entry, cfg.AttrDisplayName);
            email       = GetAttr(entry, cfg.AttrEmail);
            department  = GetAttr(entry, cfg.AttrDepartment);
        }

        // 2. Kullanıcı DN'i ile şifreyi doğrula
        using (var userConn = new LdapConnection { SecureSocketLayer = cfg.UseSsl })
        {
            userConn.Connect(cfg.Server!, cfg.Port);
            if (cfg.UseTls) userConn.StartTls();
            userConn.Bind(userDn, password); // InvalidCredentials fırlatır
        }

        // 3. Lokal kullanıcıyı bul veya oluştur
        var local = db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefault(u => u.Username == samAccount);

        if (local != null)
        {
            if (!local.IsActive)
            {
                logger.LogWarning("Pasif kullanıcı AD ile giriş denedi: {Username}", samAccount);
                Error = "Hesabınız devre dışı bırakılmış. Yönetici ile iletişime geçin.";
                return null;
            }
            return local;
        }

        if (!cfg.AutoCreateUsers)
        {
            Error = "Active Directory kullanıcısı sistemde tanımlı değil.";
            return null;
        }

        local = new Models.User
        {
            Username     = samAccount,
            FullName     = displayName ?? samAccount,
            Role         = cfg.DefaultRole,
            PasswordHash = "$ldap$", // yerel parola girişini engeller
            IsActive     = true,
        };
        db.Users.Add(local);
        db.SaveChanges();
        logger.LogInformation("LDAP auto-create: {Username}", samAccount);
        return local;
    }

    private static string GetAttr(LdapEntry entry, string attrName)
    {
        try { return entry.GetAttribute(attrName)?.StringValue ?? ""; }
        catch { return ""; }
    }

    // RFC 4515 §3 — LDAP filter değeri için özel karakter kaçış
    private static string EscapeLdapFilterValue(string value)
    {
        var sb = new StringBuilder(value.Length + 8);
        foreach (char c in value)
        {
            sb.Append(c switch
            {
                '*'  => @"\2a",
                '('  => @"\28",
                ')'  => @"\29",
                '\\' => @"\5c",
                '\0' => @"\00",
                _    => c.ToString(),
            });
        }
        return sb.ToString();
    }
}
