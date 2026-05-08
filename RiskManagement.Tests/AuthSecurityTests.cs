using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class AuthSecurityTests
{
    [Fact]
    public void GenerateResetToken_ReturnsStrongUniqueUrlSafeTokens()
    {
        var first = AuthService.GenerateResetToken();
        var second = AuthService.GenerateResetToken();

        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.False(string.IsNullOrWhiteSpace(second));
        Assert.NotEqual(first, second);
        Assert.DoesNotContain("+", first);
        Assert.DoesNotContain("/", first);
        Assert.DoesNotContain("=", first);
        Assert.True(first.Length >= 40);
    }

    [Fact]
    public void HashResetToken_DoesNotStoreRawToken()
    {
        const string token = "sample-reset-token";

        var firstHash = AuthService.HashResetToken(token);
        var secondHash = AuthService.HashResetToken(token);

        Assert.Equal(firstHash, secondHash);
        Assert.NotEqual(token, firstHash);
        Assert.Equal(64, firstHash.Length);
    }

    [Fact]
    public void PasswordResetPolicy_UsesShortLifetimeAndCooldown()
    {
        Assert.True(AuthService.PasswordResetTokenLifetime <= TimeSpan.FromMinutes(30));
        Assert.True(AuthService.PasswordResetRequestCooldown >= TimeSpan.FromMinutes(1));
    }
}
