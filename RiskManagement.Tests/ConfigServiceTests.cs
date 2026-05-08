using Microsoft.Extensions.Logging.Abstractions;
using RiskManagement.Services;
using Xunit;

namespace RiskManagement.Tests;

public class ConfigServiceRiskLevelTests
{
    private static (ConfigService Config, IRiskCalculator Calc) Build()
    {
        var db     = TestDb.Create();
        var config = new ConfigService(db, NullLogger<ConfigService>.Instance);
        var calc   = new RiskCalculator(config);
        return (config, calc);
    }

    // ── CalculateRiskLevel sınır değerleri ───────────────────────────────────

    [Theory]
    [InlineData(0.0,    "Kabul Edilebilir")]
    [InlineData(1.0,    "Kabul Edilebilir")]
    [InlineData(19.99,  "Kabul Edilebilir")]
    [InlineData(20.0,   "Orta Risk")]
    [InlineData(69.99,  "Orta Risk")]
    [InlineData(70.0,   "Önemli Risk")]
    [InlineData(199.99, "Önemli Risk")]
    [InlineData(200.0,  "Yüksek Risk")]
    [InlineData(399.99, "Yüksek Risk")]
    [InlineData(400.0,  "Çok Yüksek Risk")]
    [InlineData(9999.0, "Çok Yüksek Risk")]
    public void CalculateRiskLevel_ReturnsCorrectLevel(double score, string expected)
    {
        var (_, calc) = Build();
        Assert.Equal(expected, calc.CalculateRiskLevel(score));
    }

    [Fact]
    public void CalculateRiskLevel_AutoApproveThreshold_IsBelow70()
    {
        // RiskService.AddEvaluation: score < 70 → approved, >= 70 → under_review
        var (_, calc) = Build();
        Assert.Equal("Orta Risk",   calc.CalculateRiskLevel(69.0));
        Assert.Equal("Önemli Risk", calc.CalculateRiskLevel(70.0));
    }

    // ── Get<T> varsayılan değerler ───────────────────────────────────────────

    [Fact]
    public void Get_UnknownKey_ReturnsDefault()
    {
        var (config, _) = Build();
        var result = config.Get<string[]>("nonexistent_key");
        Assert.Null(result);
    }

    [Fact]
    public void GetList_RiskCategories_ReturnsNonEmpty()
    {
        var (config, _) = Build();
        var cats = config.GetList("risk_categories");
        Assert.NotEmpty(cats);
        Assert.Contains("Finansal", cats);
        Assert.Contains("Operasyonel", cats);
    }

    [Fact]
    public void GetList_RiskStrategies_ReturnsAllFour()
    {
        var (config, _) = Build();
        var strategies = config.GetList("risk_strategies");
        Assert.Equal(4, strategies.Length);
        Assert.Contains("Riski Azaltma", strategies);
        Assert.Contains("Riskten Kaçınma", strategies);
    }

    [Fact]
    public void IsModuleActive_DefaultsToTrue()
    {
        var (config, _) = Build();
        Assert.True(config.IsModuleActive("risk"));
        Assert.True(config.IsModuleActive("audit"));
        Assert.True(config.IsModuleActive("ethics"));
    }

    [Fact]
    public void Set_OverridesDefault_AndGetReturnsNewValue()
    {
        var db     = TestDb.Create();
        var config = new ConfigService(db, NullLogger<ConfigService>.Instance);

        config.Set("review_threshold_days", 45);
        var result = config.Get<int>("review_threshold_days");

        Assert.Equal(45, result);
    }

    [Fact]
    public void GetRiskDetailFields_ReturnsAllSixFields()
    {
        var (config, _) = Build();
        var fields = config.GetRiskDetailFields();
        Assert.Equal(6, fields.Count);
        Assert.True(fields["source_type"]);
        Assert.True(fields["hazard"]);
    }

    // ── GetRiskLevelColor — renk DB'den okunur, hardcoded değil ─────────────

    [Theory]
    [InlineData("Çok Yüksek Risk", "#7f1d1d")]
    [InlineData("Yüksek Risk",     "#dc2626")]
    [InlineData("Önemli Risk",     "#ea580c")]
    [InlineData("Orta Risk",       "#ca8a04")]
    [InlineData("Kabul Edilebilir","#16a34a")]
    [InlineData("Bilinmiyor",      "#16a34a")] // bilinmeyen seviye → fallback
    public void GetRiskLevelColor_ReturnsCorrectColor(string level, string expected)
    {
        var (_, calc) = Build();
        Assert.Equal(expected, calc.GetRiskLevelColor(level));
    }
}
