using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RiskManagement.Data;
using RiskManagement.Extensions;
using RiskManagement.Mcp;
using RiskManagement.Models;
using RiskManagement.Services;
using RiskManagement.Services.Ai;
using RiskManagement.Services.Email;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Windows Service olarak çalışmayı destekle (intranet kurulumu için)
builder.Host.UseWindowsService(opts => opts.ServiceName = "RedZone");
// Linux'ta systemd servisi olarak çalışmayı destekle
builder.Host.UseSystemd();

// Güvenlik: production ortamında JWT anahtarı zorunlu olsun (uygulama şu an cookie auth kullansa da
// konfigürasyonda gizli anahtar placeholder bırakılmamalı).
var configuredJwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("Jwt__Key")
    ?? Environment.GetEnvironmentVariable("JWT__KEY");
if (builder.Environment.IsProduction() && (string.IsNullOrEmpty(configuredJwtKey) || configuredJwtKey.StartsWith("CHANGE_THIS")))
    throw new InvalidOperationException("Production: Jwt__Key ortam değişkeni ayarlanmalıdır.");

// ─── Production placeholder guard'ları ───────────────────────────────────────
if (builder.Environment.IsProduction())
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "";
    if (connStr.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "Production: ConnectionStrings__DefaultConnection içinde 'CHANGE_ME' placeholder var. " +
            "Gerçek veritabanı şifresini ortam değişkeni olarak verin.");

    if (builder.Configuration.GetValue<bool>("AppSettings:DemoMode"))
        throw new InvalidOperationException(
            "Production: AppSettings__DemoMode=true seed verisi çalıştırır. " +
            "Üretim ortamında 'AppSettings__DemoMode=false' olarak ayarlayın.");
}


// ─── Veritabanı ───────────────────────────────────────────────────────────────
var useSqlite = builder.Configuration.GetValue<bool>("AppSettings:UseSqlite");
Action<DbContextOptionsBuilder> configureDb;
if (useSqlite)
{
    var sqlitePath = builder.Configuration.GetValue<string>("AppSettings:SqlitePath") ?? "risk_management.db";
    configureDb = opt => opt.UseSqlite($"Data Source={sqlitePath}");
}
else
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("Connection string bulunamadı.");
    configureDb = opt => opt.UseMySql(connStr, new MySqlServerVersion(new Version(8, 0, 36)));
}
// Blazor Server'da scoped context = circuit ömrü (tüm oturum). Tek paylaşımlı context,
// eşzamanlı işlemlerde "second operation started" hatasına ve bayat change-tracker'a yol açar.
// Çözüm: DbContextFactory kaydet. Geriye dönük uyum için scoped AppDbContext de fabrikadan
// üretilir — mevcut @inject AppDbContext kullanımları değişmeden çalışır; yüksek-frekanslı /
// her-zaman-açık bileşenler ise IDbContextFactory ile kısa-ömürlü, izole context alır.
builder.Services.AddDbContextFactory<AppDbContext>(configureDb);
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// ─── Auth: Cookie ─────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.LogoutPath = "/logout";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8); // DB değeri PostConfigure ile üstüne yazar
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        // Prod için cookie transport güvenliğini zorunlu kıl
        opt.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

// Oturum süresi DB'deki security_session_timeout_hours değerinden okunur.
// IOptionsMonitor sayesinde uygulama yeniden başlatıldığında güncel değeri alır.
builder.Services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>(sp =>
    new Microsoft.Extensions.Options.PostConfigureOptions<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(
        CookieAuthenticationDefaults.AuthenticationScheme,
        opt =>
        {
            var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
            using var scope = scopeFactory.CreateScope();
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var row = db.AppConfigs.AsNoTracking()
                    .FirstOrDefault(c => c.Key == "security_session_timeout_hours");
                if (row?.Value is not null
                    && int.TryParse(row.Value.Trim('"'), out var h)
                    && h > 0)
                    opt.ExpireTimeSpan = TimeSpan.FromHours(h);
            }
            catch { /* migration henüz çalışmadıysa varsayılan 8h kalır */ }
        }));

// ─── Rate Limiting ────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    // Login sayfası: 1 dakikada 10 deneme, sonrasında 429
    opts.AddSlidingWindowLimiter("login", o =>
    {
        o.PermitLimit       = 10;
        o.Window            = TimeSpan.FromMinutes(1);
        o.SegmentsPerWindow = 2;
        o.QueueLimit        = 0;
    });
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Blazor Server anonim risk sayfası için IP tabanlı rate limiting (IMemoryCache)
// Gerçek rate limiter middleware Blazor SignalR bağlantılarına uygulanamaz.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RiskManagement.Services.AnonymousRateLimiter>();

// ─── Antiforgery ──────────────────────────────────────────────────────────────
builder.Services.AddAntiforgery();

// ─── Health Checks ────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// ─── Blazor Server ───────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// ─── Services ─────────────────────────────────────────────────────────────────
// ─── E-posta Servisi ─────────────────────────────────────────────────────────
// Ayarlar DB'de saklanır (ConfigService), appsettings.json'a gerek kalmaz.
// Admin paneli → Sistem Yapılandırması → E-posta üzerinden yönetilir.
// ─── In-memory log buffer (singleton) ────────────────────────────────────────
var logBuffer = new RecentLogBuffer();
builder.Services.AddSingleton(logBuffer);
builder.Logging.AddProvider(new RecentLogBufferProvider(logBuffer));

builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddHostedService<ActionReminderWorker>();
builder.Services.AddScoped<INotificationService, SmtpNotificationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ConfigService>();
builder.Services.AddScoped<IRiskCalculator, RiskCalculator>();
builder.Services.AddScoped<RiskService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ExternalAuditService>();
builder.Services.AddScoped<EthicsService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<OrgService>();
builder.Services.AddScoped<RiskLibraryService>();
builder.Services.AddScoped<McpApiKeyService>();
builder.Services.AddScoped<McpRequestContext>();

// ─── AI Servisleri ────────────────────────────────────────────────────────────
// Provider seçimi ConfigService'ten okunur — her şirket kendi ayarını girer.
builder.Services.AddHttpClient();
builder.Services.AddScoped<IAiCompletionService>(sp =>
{
    var cfg      = sp.GetRequiredService<ConfigService>();
    var settings = cfg.GetAiSettings();
    var http     = sp.GetRequiredService<IHttpClientFactory>();
    return settings.Provider switch
    {
        "anthropic"                        => new AnthropicCompletionService(settings, http),
        // "openai_compatible" yeni evrensel protokol adı;
        // eski provider adları (openai/gemini/deepseek/ollama) geriye dönük uyumluluk için korunuyor.
        "openai_compatible" or "openai" or "gemini" or "deepseek" or "ollama"
            => new OpenAiCompatibleCompletionService(settings, http),
        _                                  => new NullCompletionService()
    };
});
builder.Services.AddScoped<AiRiskDraftService>();

// ─── MCP Server ───────────────────────────────────────────────────────────────
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<RiskManagementTools>();

// HTTP context erişimi için (Blazor Server'da gerekli)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ─── Migrations + Seed ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Migration 30 saniyede tamamlanmazsa uyarı ver; production'da kritik hata uygulamayı durdurur.
    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
    Exception? migrationError = null;
    try
    {
        await db.Database.MigrateAsync(cts.Token);
        logger.LogInformation("Migration tamamlandı.");
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning("Migration 30 saniyede tamamlanamadı. /admin/db-migrate ile manuel çalıştırın.");
    }
    catch (Exception ex)
    {
        migrationError = ex;
        logger.LogError(ex, "Migration hatası: {Message}", ex.Message);
    }

    // Production'da migration hatası uygulamayı durdurur — şema uyumsuzluğuyla çalışmak tehlikeli.
    if (migrationError != null && app.Environment.IsProduction())
        throw new InvalidOperationException(
            "Production ortamında migration başarısız. Veritabanını kontrol edin ve yeniden başlatın.",
            migrationError);

    try
    {
        if (app.Configuration.GetValue<bool>("AppSettings:DemoMode"))
            await SeedData.RunAsync(db);

        // Demo verisi sıfırlama — AppSettings:DemoReset=true ve sürüm değiştiğinde çalışır
        if (app.Configuration.GetValue<bool>("AppSettings:DemoReset"))
        {
            var seedVer = db.AppConfigs.FirstOrDefault(c => c.Key == "demo_seed_version")?.Value;
            if (seedVer != DemoSeedData.SeedVersion)
            {
                logger.LogInformation("Demo verisi sıfırlanıyor (v{V})…", DemoSeedData.SeedVersion);
                await DemoSeedData.ResetAndSeedAsync(db);
                db.AppConfigs.RemoveRange(db.AppConfigs.Where(c => c.Key == "demo_seed_version"));
                db.AppConfigs.Add(new AppConfig { Key = "demo_seed_version", Value = DemoSeedData.SeedVersion });
                db.SaveChanges();
                logger.LogInformation("Demo verisi tamamlandı.");
            }
        }

        scope.ServiceProvider.GetRequiredService<RiskLibraryService>().SeedIfEmpty();
        AuthService.SeedSystemData(db);
        SyncUserAssignments(db);

        // TrackingToken backfill — token kolonu eklenmeden önce oluşturulmuş etik
        // bildirimlerine gizli takip token'ı atar (idempotent: yalnızca NULL olanlar).
        var tokenless = db.EthicsReports.Where(r => r.TrackingToken == null).ToList();
        if (tokenless.Count > 0)
        {
            foreach (var r in tokenless)
                r.TrackingToken = EthicsService.GenerateTrackingToken();
            db.SaveChanges();
            logger.LogInformation("{Count} etik bildirimine takip token'ı atandı.", tokenless.Count);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Seed/sync hatası: {Message}", ex.Message);
    }
}

// ─── Middleware ───────────────────────────────────────────────────────────────

// Ters proxy (nginx / AWS ALB) arkasındaki deploy'larda X-Forwarded-Proto başlığından
// scheme'i doğru oku. Aksi hâlde auth redirect URL'leri http:// ile üretilir.
// Development ortamında da zararsızdır; bayrak sıfırlanmaz.
if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ─── Güvenlik response header'ları ────────────────────────────────────────────
// Clickjacking, MIME sniffing ve bilgi sızıntısı önleme.
// CSP notu: Blazor Server, SignalR WebSocket + inline script (_framework/blazor.server.js)
// gerektirdiğinden katı bir CSP zorlu; mevcut yapıda 'unsafe-inline' kaçınılmaz.
// Yükseltme yolu: nonce-tabanlı CSP + script-src 'nonce-{değer}' (ileride değerlendirilebilir).
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"]        = "SAMEORIGIN";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    // Temel CSP: framing ve plugin kaynağını sınırla; script/style kaynakları Blazor
    // gereksinimleri nedeniyle self + unsafe-inline ile kısıtlı tutuldu.
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self' ws: wss:; " +  // Blazor SignalR WebSocket
        "frame-ancestors 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self';";
    await next();
});

// Hassas attachment dizinlerine doğrudan erişimi engelle.
// Auth gerektirmez — her zaman erken çalışmalı.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/findings") ||
        context.Request.Path.StartsWithSegments("/uploads/ethics"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

// Kapalı modüllerin sayfalarına erişimi engelle.
// UseAuthentication()'dan SONRA gelir — context.User burada dolu olur.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? "";
        var isInfra = path.StartsWith("/_blazor") || path.StartsWith("/_framework")
                   || path.StartsWith("/_content") || path.StartsWith("/css")
                   || path.StartsWith("/js") || path.StartsWith("/lib");
        if (!isInfra)
        {
            var cache = context.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();

            // Modül durumu 60 saniye önbelleğe alınır — her request'te DB'ye vurulmaz
            bool IsDisabled(string module) => cache.GetOrCreate($"module_guard:{module}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                using var s = context.RequestServices.CreateScope();
                return !s.ServiceProvider.GetRequiredService<RiskManagement.Services.ConfigService>().IsModuleActive(module);
            });

            var isAnonRisk   = path.Equals("/risk/propose", StringComparison.OrdinalIgnoreCase);
            var isAnonEthics = path.StartsWith("/ethics/submit", StringComparison.OrdinalIgnoreCase)
                            || path.StartsWith("/ethics/status", StringComparison.OrdinalIgnoreCase);

            if ((!isAnonRisk   && path.StartsWith("/risk",   StringComparison.OrdinalIgnoreCase) && IsDisabled("risk"))  ||
                (                 path.StartsWith("/audit",  StringComparison.OrdinalIgnoreCase) && IsDisabled("audit")) ||
                (!isAnonEthics && path.StartsWith("/ethics", StringComparison.OrdinalIgnoreCase) && IsDisabled("ethics")))
            {
                context.Response.Redirect("/?modul_kapali=1");
                return;
            }
        }
    }
    await next();
});

// ─── MCP endpoint — API key ile korumalı ─────────────────────────────────────
app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/mcp"))
    {
        await next();
        return;
    }

    var rawKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
        ?? context.Request.Headers["Authorization"].FirstOrDefault()
               ?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    if (string.IsNullOrEmpty(rawKey))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "X-Api-Key header zorunludur" });
        return;
    }

    using var scope = context.RequestServices.CreateScope();
    var keySvc = scope.ServiceProvider.GetRequiredService<McpApiKeyService>();
    var apiKey = keySvc.Validate(rawKey);
    if (apiKey is null)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "Geçersiz API anahtarı" });
        return;
    }

    // Scope user'ı request context'ine aktar
    var mcpCtx = context.RequestServices.GetRequiredService<McpRequestContext>();
    mcpCtx.ScopeUser = apiKey.ScopeUser;

    await next();
});

app.MapHealthChecks("/healthz");
app.MapRazorPages();
app.MapBlazorHub();
app.MapExportEndpoints();
app.MapImportEndpoints();
app.MapMcp("/mcp");

app.MapFallbackToPage("/_Host");

app.Run();

// Mevcut User.Role / DepartmentId / OrganizationId / CompanyId verilerinden
// yeni çoklu atama tablolarını doldurur. Her tablo ve her kullanıcı idempotent olarak
// ayrı ayrı kontrol edilir — global erken dönüş kaldırıldı çünkü sonradan eklenen
// kullanıcılar ya da yarım kalan migration'lar atlanabiliyordu.
static void SyncUserAssignments(AppDbContext db)
{
    // Rol ataması eksik kullanıcılar
    var needsRole = db.Users
        .Where(u => !string.IsNullOrEmpty(u.Role) && !db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleName == u.Role))
        .Select(u => new { u.Id, u.Role })
        .ToList();
    foreach (var u in needsRole)
        db.UserRoles.Add(new UserRole { UserId = u.Id, RoleName = u.Role! });

    // Departman ataması eksik kullanıcılar
    var needsDept = db.Users
        .Where(u => u.DepartmentId != null && !db.UserDepartments.Any(ud => ud.UserId == u.Id && ud.DepartmentId == u.DepartmentId))
        .Select(u => new { u.Id, u.DepartmentId })
        .ToList();
    foreach (var u in needsDept)
        db.UserDepartments.Add(new UserDepartment { UserId = u.Id, DepartmentId = u.DepartmentId!.Value });

    // Organizasyon ataması eksik kullanıcılar
    var needsOrg = db.Users
        .Where(u => u.OrganizationId != null && !db.UserOrganizations.Any(uo => uo.UserId == u.Id && uo.OrganizationId == u.OrganizationId))
        .Select(u => new { u.Id, u.OrganizationId })
        .ToList();
    foreach (var u in needsOrg)
        db.UserOrganizations.Add(new UserOrganization { UserId = u.Id, OrganizationId = u.OrganizationId!.Value });

    // Şirket ataması eksik kullanıcılar
    var needsComp = db.Users
        .Where(u => u.CompanyId != null && !db.UserCompanies.Any(uc => uc.UserId == u.Id && uc.CompanyId == u.CompanyId))
        .Select(u => new { u.Id, u.CompanyId })
        .ToList();
    foreach (var u in needsComp)
        db.UserCompanies.Add(new UserCompany { UserId = u.Id, CompanyId = u.CompanyId!.Value });

    db.SaveChanges();
}
