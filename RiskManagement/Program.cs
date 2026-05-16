using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;
using RiskManagement.Data;
using RiskManagement.Models;
using RiskManagement.Services;
using RiskManagement.Services.Email;

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
if (useSqlite)
{
    var sqlitePath = builder.Configuration.GetValue<string>("AppSettings:SqlitePath") ?? "risk_management.db";
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseSqlite($"Data Source={sqlitePath}"));
}
else
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
        ?? throw new InvalidOperationException("Connection string bulunamadı.");
    builder.Services.AddDbContext<AppDbContext>(opt =>
        opt.UseMySql(connStr, new MySqlServerVersion(new Version(8, 0, 36))));
}

// ─── Auth: Cookie ─────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.LogoutPath = "/logout";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        // Prod için cookie transport güvenliğini zorunlu kıl
        opt.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

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
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailWorker>();
builder.Services.AddScoped<SmtpNotificationService>();
builder.Services.AddScoped<INotificationService, SmtpNotificationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ConfigService>();
builder.Services.AddScoped<IRiskCalculator, RiskCalculator>();
builder.Services.AddScoped<RiskService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<EthicsService>();
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<RiskLibraryService>();

// HTTP context erişimi için (Blazor Server'da gerekli)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ─── MySQL Schema Fix (raw SQL — EF Core modelinden bağımsız) ────────────────
// EF Core migration çalışmadan önce eksik kolon/tablo varsa direkt ekler.
// SQLite ortamında çalışmaz (MySQL dışı ortamlar atlanır).
if (!builder.Configuration.GetValue<bool>("AppSettings:UseSqlite"))
{
    var fixLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var fixConnStr = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? "";
    if (!string.IsNullOrEmpty(fixConnStr))
    {
        try
        {
            using var fixConn = new MySqlConnector.MySqlConnection(fixConnStr);
            fixConn.Open();
            var fixCmds = new[]
            {
                "ALTER TABLE `Risks` ADD COLUMN IF NOT EXISTS `SourceLibraryItemId` int NULL",

                @"CREATE TABLE IF NOT EXISTS `AuditPlans` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `Year` int NOT NULL DEFAULT 0,
                    `Title` varchar(200) NOT NULL DEFAULT '',
                    `Description` longtext NULL,
                    `CreatedById` int NOT NULL DEFAULT 0,
                    `CreatedAt` datetime(6) NOT NULL DEFAULT '2000-01-01 00:00:00',
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET utf8mb4",

                @"CREATE TABLE IF NOT EXISTS `AuditPlanItems` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `AuditPlanId` int NOT NULL DEFAULT 0,
                    `Title` varchar(200) NOT NULL DEFAULT '',
                    `AuditType` varchar(100) NULL,
                    `AuditedUnit` varchar(200) NULL,
                    `Description` varchar(500) NULL,
                    `PlannedStartDate` date NOT NULL DEFAULT '2000-01-01',
                    `PlannedEndDate` date NOT NULL DEFAULT '2000-01-01',
                    `ActualStartDate` date NULL,
                    `ActualEndDate` date NULL,
                    `DepartmentId` int NULL,
                    `ResponsibleId` int NULL,
                    `Notes` varchar(500) NULL,
                    `SortOrder` int NOT NULL DEFAULT 0,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET utf8mb4",

                "ALTER TABLE `AuditPlans`    MODIFY COLUMN `Id` int NOT NULL AUTO_INCREMENT",
                "ALTER TABLE `AuditPlanItems` MODIFY COLUMN `Id` int NOT NULL AUTO_INCREMENT",

                "ALTER TABLE `AuditPlanItems`  ADD COLUMN IF NOT EXISTS `DepartmentId`    int NULL",
                "ALTER TABLE `InternalAudits`  ADD COLUMN IF NOT EXISTS `DepartmentId`    int NULL",
                "ALTER TABLE `InternalAudits`  ADD COLUMN IF NOT EXISTS `AuditPlanItemId` int NULL",

                @"INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES
                    ('20260514183416_AddSourceLibraryItemIdToRisk','8.0.2'),
                    ('20260514184823_AddAuditPlan','8.0.2'),
                    ('20260514190109_AddAuditPlanItemDepartment','8.0.2'),
                    ('20260514190916_AddAuditDeptAndPlanLink','8.0.2'),
                    ('20260514200000_FixMySqlAutoIncrement','8.0.2')",
            };
            foreach (var sql in fixCmds)
            {
                try
                {
                    using var cmd = new MySqlConnector.MySqlCommand(sql, fixConn);
                    cmd.CommandTimeout = 20;
                    cmd.ExecuteNonQuery();
                }
                catch (Exception ex) { fixLogger.LogWarning("Fix SQL atlandı: {Msg}", ex.Message); }
            }
            fixLogger.LogInformation("Schema fix tamamlandı.");
        }
        catch (Exception ex) { fixLogger.LogError(ex, "Schema fix bağlantı hatası."); }
    }
}

// ─── Migrations + Seed ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Migration 30 saniyede tamamlanmazsa uyarı ver, devam et
    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
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
        logger.LogError(ex, "Migration hatası: {Message}", ex.Message);
    }

    try
    {
        if (app.Configuration.GetValue<bool>("AppSettings:DemoMode"))
            await SeedData.RunAsync(db);

        scope.ServiceProvider.GetRequiredService<RiskLibraryService>().SeedIfEmpty();
        SyncUserAssignments(db);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Seed/sync hatası: {Message}", ex.Message);
    }
}

// ─── Middleware ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads"))
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

app.MapHealthChecks("/healthz");
app.MapRazorPages();
app.MapBlazorHub();
// ─── Export Endpoints ─────────────────────────────────────────────────────────
app.MapGet("/export/risks/excel", (RiskService riskSvc, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var risks = riskSvc.GetForUser(userObj.Id, userObj.Role);
    var bytes = exportSvc.ExportRisksToExcel(risks);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Risk_Kaydi_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

app.MapGet("/export/risks/pdf", (RiskService riskSvc, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var risks = riskSvc.GetForUser(userObj.Id, userObj.Role);
    var bytes = exportSvc.ExportRisksToPdf(risks);
    return Results.File(bytes, "application/pdf",
        $"Risk_Kaydi_{DateTime.Now:yyyyMMdd}.pdf");
}).RequireAuthorization();

app.MapGet("/export/findings/excel", (AuditService auditSvc, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var findings = auditSvc.GetFindingsForUser(userObj.Id, userObj.Role);
    var bytes = exportSvc.ExportFindingsToExcel(findings);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Denetim_Bulgulari_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

app.MapGet("/export/findings/pdf", (AuditService auditSvc, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var findings = auditSvc.GetFindingsForUser(userObj.Id, userObj.Role);
    var bytes = exportSvc.ExportFindingsToPdf(findings);
    return Results.File(bytes, "application/pdf",
        $"Denetim_Bulgulari_{DateTime.Now:yyyyMMdd}.pdf");
}).RequireAuthorization();

// ── Kontrol Planı Export ──────────────────────────────────────────────────────
app.MapGet("/export/controls/excel", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var controls = db.Controls
        .Include(c => c.Risk)
        .Include(c => c.EnteredBy)
        .Include(c => c.OwnerDept)
        .ToList();
    var bytes = exportSvc.ExportControlsToExcel(controls);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Kontrol_Plani_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

app.MapGet("/export/controls/pdf", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var controls = db.Controls
        .Include(c => c.Risk)
        .Include(c => c.EnteredBy)
        .Include(c => c.OwnerDept)
        .ToList();
    var bytes = exportSvc.ExportControlsToPdf(controls);
    return Results.File(bytes, "application/pdf",
        $"Kontrol_Plani_{DateTime.Now:yyyyMMdd}.pdf");
}).RequireAuthorization();

// ── Risk Aksiyon Planları Export ──────────────────────────────────────────────
app.MapGet("/export/action-plans/excel", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var plans = db.ActionPlans
        .Include(a => a.Risk)
        .Include(a => a.CreatedBy)
        .Include(a => a.OwnerDept)
        .ToList();
    var bytes = exportSvc.ExportActionPlansToExcel(plans);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Aksiyon_Planlari_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

app.MapGet("/export/action-plans/pdf", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var plans = db.ActionPlans
        .Include(a => a.Risk)
        .Include(a => a.CreatedBy)
        .Include(a => a.OwnerDept)
        .ToList();
    var bytes = exportSvc.ExportActionPlansToPdf(plans);
    return Results.File(bytes, "application/pdf",
        $"Aksiyon_Planlari_{DateTime.Now:yyyyMMdd}.pdf");
}).RequireAuthorization();

// ── Denetim Aksiyon Planları Export ──────────────────────────────────────────
app.MapGet("/export/audit-actions/excel", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var actions = db.AuditFindingActions
        .Include(a => a.Finding)
        .Include(a => a.CreatedBy)
        .ToList();
    var bytes = exportSvc.ExportAuditActionsToExcel(actions);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Denetim_Aksiyonlari_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

app.MapGet("/export/audit-actions/pdf", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var actions = db.AuditFindingActions
        .Include(a => a.Finding)
        .Include(a => a.CreatedBy)
        .ToList();
    var bytes = exportSvc.ExportAuditActionsToPdf(actions);
    return Results.File(bytes, "application/pdf",
        $"Denetim_Aksiyonlari_{DateTime.Now:yyyyMMdd}.pdf");
}).RequireAuthorization();

// ── Etik Bildirimler Export ───────────────────────────────────────────────────
app.MapGet("/export/ethics/excel", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var reports = db.EthicsReports.ToList();
    var bytes = exportSvc.ExportEthicsToExcel(reports);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Etik_Bildirimler_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

app.MapGet("/export/ethics/pdf", (AppDbContext db, ExportService exportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();
    var reports = db.EthicsReports.ToList();
    var bytes = exportSvc.ExportEthicsToPdf(reports);
    return Results.File(bytes, "application/pdf",
        $"Etik_Bildirimler_{DateTime.Now:yyyyMMdd}.pdf");
}).RequireAuthorization();

// ── Risk Kütüphanesi Export ───────────────────────────────────────────────────
app.MapGet("/export/library/excel", (RiskLibraryService libSvc, ClaimsPrincipal user) =>
{
    if (AuthService.UserFromPrincipal(user) is null) return Results.Unauthorized();
    var items = libSvc.Search(null, null, null, null);
    var bytes = libSvc.ExportToExcel(items);
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        $"Risk_Kutuphanesi_{DateTime.Now:yyyyMMdd}.xlsx");
}).RequireAuthorization();

// ── Şablon İndirme ────────────────────────────────────────────────────────────
app.MapGet("/import/template/{module}", (string module, ExportService exportSvc, RiskLibraryService libSvc, ClaimsPrincipal user) =>
{
    if (AuthService.UserFromPrincipal(user) is null) return Results.Unauthorized();
    var (bytes, fileName) = module switch
    {
        "risks"        => (exportSvc.GetRiskImportTemplate(),        "Risk_Sablon.xlsx"),
        "controls"     => (exportSvc.GetControlImportTemplate(),     "Kontrol_Sablon.xlsx"),
        "action-plans" => (exportSvc.GetActionPlanImportTemplate(),  "AksiyonPlan_Sablon.xlsx"),
        "findings"     => (exportSvc.GetFindingImportTemplate(),     "Bulgu_Sablon.xlsx"),
        "audit-actions"=> (exportSvc.GetAuditActionImportTemplate(), "DenetimAksiyon_Sablon.xlsx"),
        "ethics"       => (exportSvc.GetEthicsImportTemplate(),      "Etik_Sablon.xlsx"),
        "library"      => (libSvc.GetImportTemplate(),               "Kutuphane_Sablon.xlsx"),
        _              => ((byte[]?)null, "")
    };
    if (bytes is null) return Results.NotFound();
    return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
}).RequireAuthorization();

// ── İçe Aktarma ───────────────────────────────────────────────────────────────
app.MapPost("/import/{module}", async (string module, HttpRequest request, ImportService importSvc, RiskLibraryService libraryImportSvc, ClaimsPrincipal user) =>
{
    var userObj = AuthService.UserFromPrincipal(user);
    if (userObj is null) return Results.Unauthorized();

    if (!request.HasFormContentType || request.Form.Files.Count == 0)
        return Results.BadRequest(new { error = "Dosya yüklenmedi." });

    var file = request.Form.Files[0];
    if (file.Length == 0)
        return Results.BadRequest(new { error = "Boş dosya." });

    using var stream = new MemoryStream();
    await file.CopyToAsync(stream);
    stream.Position = 0;

    ImportResult result = module switch
    {
        "risks"         => importSvc.ImportRisksFromExcel(stream, userObj.Id),
        "controls"      => importSvc.ImportControlsFromExcel(stream, userObj.Id),
        "action-plans"  => importSvc.ImportActionPlansFromExcel(stream, userObj.Id),
        "findings"      => importSvc.ImportFindingsFromExcel(stream, userObj.Id),
        "audit-actions" => importSvc.ImportAuditActionsFromExcel(stream, userObj.Id),
        "ethics"        => importSvc.ImportEthicsFromExcel(stream),
        "library"       => libraryImportSvc.ImportFromExcel(stream, userObj.Id),
        _               => new ImportResult { Errors = ["Geçersiz modül."] }
    };

    return Results.Ok(result);
}).RequireAuthorization().DisableAntiforgery();

app.MapGet("/audit/findings/{findingId:int}/attachments/{attachmentId:int}/download",
    async (int findingId, int attachmentId, AppDbContext db, IWebHostEnvironment env, ClaimsPrincipal user) =>
    {
        try
        {
            var attachment = await db.FindingAttachments
                .Include(a => a.Finding).ThenInclude(f => f.InternalAudit)
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.FindingId == findingId);
            if (attachment == null) return Results.NotFound();
            if (!AuditService.CanDownloadFindingFile(user, attachment.Finding)) return Results.Forbid();

            var resolved = AuditService.ResolveAttachmentPath(attachment, env.ContentRootPath, env.WebRootPath);
            return resolved is null
                ? Results.NotFound()
                : Results.File(resolved.Value.StoredPath!, "application/octet-stream", resolved.Value.FileName);
        }
        catch (Exception ex)
        {
            var log = app.Services.GetRequiredService<ILogger<Program>>();
            log.LogError(ex, "Attachment download failed: findingId={FindingId} attachmentId={AttachmentId}", findingId, attachmentId);
            return Results.Problem("Dosya indirme sırasında hata oluştu.", statusCode: 500);
        }
    })
    .RequireAuthorization();

app.MapGet("/audit/findings/{findingId:int}/closure-files/{fileName}",
    async (int findingId, string fileName, AppDbContext db, IWebHostEnvironment env, ClaimsPrincipal user) =>
    {
        try
        {
            var finding = await db.AuditFindings
                .Include(f => f.InternalAudit)
                .FirstOrDefaultAsync(f => f.Id == findingId);
            if (finding == null) return Results.NotFound();
            if (!AuditService.CanDownloadFindingFile(user, finding)) return Results.Forbid();
            if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
                return Results.BadRequest();

            var resolved = AuditService.ResolveClosureFilePath(findingId, fileName, env.ContentRootPath, env.WebRootPath);
            return resolved is null
                ? Results.NotFound()
                : Results.File(resolved.Value.StoredPath!, "application/octet-stream", resolved.Value.FileName);
        }
        catch (Exception ex)
        {
            var log = app.Services.GetRequiredService<ILogger<Program>>();
            log.LogError(ex, "Closure file download failed: findingId={FindingId} fileName={FileName}", findingId, fileName);
            return Results.Problem("Dosya indirme sırasında hata oluştu.", statusCode: 500);
        }
    })
    .RequireAuthorization();
app.MapFallbackToPage("/_Host");

app.Run();

// Mevcut User.Role / DepartmentId / OrganizationId / CompanyId verilerinden
// yeni çoklu atama tablolarını doldurur. Her startup'ta çalışır, idempotent.
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
