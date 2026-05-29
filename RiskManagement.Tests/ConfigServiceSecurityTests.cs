using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

/// <summary>
/// ConfigService güvenlik, kilitleme ve maskeleme testleri.
/// </summary>
public class ConfigServiceSecurityTests
{
    private static ConfigService Build()
    {
        var db = TestDb.Create();
        return new ConfigService(db, NullLogger<ConfigService>.Instance);
    }

    // ── Kilitleme ayarları ───────────────────────────────────────────────────

    [Fact]
    public void GetMaxFailedLogins_Default_IsZero()
    {
        Assert.Equal(0, Build().GetMaxFailedLogins()); // 0 = sınırsız
    }

    [Fact]
    public void GetLockoutMinutes_Default_Is15()
    {
        Assert.Equal(15, Build().GetLockoutMinutes());
    }

    [Fact]
    public void GetMaxFailedLogins_AfterSet_ReturnsSavedValue()
    {
        var cfg = Build();
        cfg.Set("security_max_failed_logins", 5);
        Assert.Equal(5, cfg.GetMaxFailedLogins());
    }

    [Fact]
    public void GetLockoutMinutes_AfterSet_ReturnsSavedValue()
    {
        var cfg = Build();
        cfg.Set("security_lockout_minutes", 30);
        Assert.Equal(30, cfg.GetLockoutMinutes());
    }

    // ── Şifre validasyonu ────────────────────────────────────────────────────

    [Fact]
    public void ValidatePassword_TooShort_ReturnsError()
    {
        var cfg = Build();
        cfg.Set("security_password_min_length", 8);
        var result = cfg.ValidatePassword("abc");
        Assert.NotNull(result);
        Assert.Contains("8", result);
    }

    [Fact]
    public void ValidatePassword_LongEnough_ReturnsNull()
    {
        var cfg = Build();
        cfg.Set("security_password_min_length", 6);
        Assert.Null(cfg.ValidatePassword("password123"));
    }

    [Fact]
    public void ValidatePassword_ComplexityOn_NoUppercase_ReturnsError()
    {
        var cfg = Build();
        cfg.Set("security_password_complexity", true);
        var result = cfg.ValidatePassword("password123");
        Assert.NotNull(result);
        Assert.Contains("büyük harf", result);
    }

    [Fact]
    public void ValidatePassword_ComplexityOn_NoDigit_ReturnsError()
    {
        var cfg = Build();
        cfg.Set("security_password_complexity", true);
        var result = cfg.ValidatePassword("Password");
        Assert.NotNull(result);
        Assert.Contains("rakam", result);
    }

    [Fact]
    public void ValidatePassword_ComplexityOn_Valid_ReturnsNull()
    {
        var cfg = Build();
        cfg.Set("security_password_complexity", true);
        Assert.Null(cfg.ValidatePassword("Password1"));
    }

    // ── Modül ayarları ───────────────────────────────────────────────────────

    [Fact]
    public void IsModuleActive_AfterDisable_ReturnsFalse()
    {
        var cfg = Build();
        cfg.Set("module_risk", false);
        Assert.False(cfg.IsModuleActive("risk"));
    }

    [Fact]
    public void IsModuleActive_AfterEnable_ReturnsTrue()
    {
        var cfg = Build();
        cfg.Set("module_audit", false);
        cfg.Set("module_audit", true);
        Assert.True(cfg.IsModuleActive("audit"));
    }

    // ── Email olay toggleları ────────────────────────────────────────────────

    [Fact]
    public void IsEmailEventEnabled_Default_ReturnsTrue()
    {
        var cfg = Build();
        Assert.True(cfg.IsEmailEventEnabled("risk_proposed"));
        Assert.True(cfg.IsEmailEventEnabled("status_changed"));
        Assert.True(cfg.IsEmailEventEnabled("owner_assigned"));
        Assert.True(cfg.IsEmailEventEnabled("action_due"));
    }

    [Fact]
    public void IsEmailEventEnabled_AfterDisable_ReturnsFalse()
    {
        var cfg = Build();
        cfg.Set("email_event_risk_proposed", false);
        Assert.False(cfg.IsEmailEventEnabled("risk_proposed"));
    }

    // ── Aksiyon zorunlulukları ───────────────────────────────────────────────

    [Fact]
    public void IsActionFieldRequired_Default_ReturnsFalse()
    {
        var cfg = Build();
        Assert.False(cfg.IsActionFieldRequired("due_date"));
        Assert.False(cfg.IsActionFieldRequired("responsible"));
        Assert.False(cfg.IsActionFieldRequired("closure_note"));
        Assert.False(cfg.IsActionFieldRequired("delay_reason"));
    }

    [Fact]
    public void IsActionFieldRequired_AfterEnable_ReturnsTrue()
    {
        var cfg = Build();
        cfg.Set("action_require_due_date", true);
        Assert.True(cfg.IsActionFieldRequired("due_date"));
    }

    // ── GetAll hassas değer davranışı ────────────────────────────────────────

    [Fact]
    public void GetAll_ReturnsAllDefaultKeys()
    {
        var all = Build().GetAll();
        Assert.True(all.ContainsKey("risk_categories"));
        Assert.True(all.ContainsKey("module_risk"));
        Assert.True(all.ContainsKey("security_max_failed_logins"));
        Assert.True(all.ContainsKey("email_settings"));
    }
}
