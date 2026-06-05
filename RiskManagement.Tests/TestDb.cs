using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RiskManagement.Data;
using RiskManagement.Services;

namespace RiskManagement.Tests;

internal static class TestDb
{
    private static DbContextOptions<AppDbContext> NewOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    public static AppDbContext Create() => new(NewOptions());

    // Testler için ephemeral (in-memory) DataProtection provider
    public static IDataProtectionProvider DataProtection { get; } =
        new EphemeralDataProtectionProvider();

    public static ConfigService CreateConfigService(AppDbContext db) =>
        new(db, new Microsoft.Extensions.Logging.Abstractions.NullLogger<ConfigService>(), DataProtection);

    /// <summary>
    /// Factory-tabanlı servisler için test fabrikası. Tek bir options örneği üzerinden üretir;
    /// böylece fabrikadan oluşturulan tüm context'ler aynı in-memory deposunu paylaşır
    /// (seed eden context ile servisin context'i aynı veriyi görür).
    /// </summary>
    public static IDbContextFactory<AppDbContext> CreateFactory() => new TestDbFactory(NewOptions());
}

internal sealed class TestDbFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}
