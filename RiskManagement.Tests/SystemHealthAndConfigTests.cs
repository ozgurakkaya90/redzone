using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class SystemHealthAndConfigTests
{
    private static ConfigService Build()
    {
        var db = TestDb.Create();
        return TestDb.CreateConfigService(db);
    }

    [Fact]
    public void GetPasswordMinLength_DefaultsToSix()
    {
        var config = Build();
        Assert.Equal(6, config.GetPasswordMinLength());
    }

    [Fact]
    public void GetPasswordMinLength_CanBeOverridden()
    {
        var config = Build();
        config.Set("security_password_min_length", 8);
        Assert.Equal(8, config.GetPasswordMinLength());
    }

    [Fact]
    public void ValidatePassword_ChecksMinimumLength()
    {
        var config = Build();
        config.Set("security_password_min_length", 8);

        // Under 8 characters should fail
        var result1 = config.ValidatePassword("abc12");
        Assert.NotNull(result1);
        Assert.Contains("en az 8 karakter", result1);

        // 8 characters should pass under default complexity (complexity is false by default)
        var result2 = config.ValidatePassword("abcdefgh");
        Assert.Null(result2);
    }

    [Fact]
    public void ValidatePassword_ChecksComplexity_WhenEnabled()
    {
        var config = Build();
        config.Set("security_password_min_length", 6);
        config.Set("security_password_complexity", true);

        // No uppercase, no digit -> should fail
        var res1 = config.ValidatePassword("abcdef");
        Assert.NotNull(res1);
        Assert.Contains("en az bir büyük harf", res1);

        // Has uppercase, but no digit -> should fail
        var res2 = config.ValidatePassword("Abcdef");
        Assert.NotNull(res2);
        Assert.Contains("en az bir rakam", res2);

        // Has uppercase and digit -> should pass
        var res3 = config.ValidatePassword("Abcde1");
        Assert.Null(res3);
    }

    [Fact]
    public void GetSessionTimeoutAndLockoutDefaults_ReturnCorrectValues()
    {
        var config = Build();
        Assert.Equal(8, config.GetSessionTimeoutHours());
        Assert.Equal(30, config.GetResetTokenMinutes());
        Assert.Equal(0, config.GetMaxFailedLogins());
        Assert.Equal(15, config.GetLockoutMinutes());
    }
}
