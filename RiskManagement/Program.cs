using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using RiskManagement.Data;
using RiskManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// ─── Veritabanı ───────────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("Connection string bulunamadı.");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(connStr));

// ─── Auth: Cookie ─────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.LogoutPath = "/logout";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

// ─── Blazor Server ───────────────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ConfigService>();
builder.Services.AddScoped<RiskService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<EthicsService>();

// HTTP context erişimi için (Blazor Server'da gerekli)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ─── Migrations + Seed ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (app.Configuration.GetValue<bool>("AppSettings:DemoMode"))
    {
        await SeedData.RunAsync(db);
    }
}

// ─── Middleware ───────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
