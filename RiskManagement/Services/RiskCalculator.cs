using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Fine-Kinney risk hesaplama mantığı.
/// Tüm eşik ve renk bilgileri ConfigService aracılığıyla DB'den okunur.
/// </summary>
public sealed class RiskCalculator(ConfigService config) : IRiskCalculator
{
    private const string FallbackLabel = "Kabul Edilebilir";
    private const string FallbackColor = "#16a34a";

    public string CalculateRiskLevel(double score) =>
        GetRiskLevelConfig(score)?.Label ?? FallbackLabel;

    public string GetRiskLevelColor(string label) =>
        GetLevels()?.FirstOrDefault(l => l.Label == label)?.Color ?? FallbackColor;

    public RiskLevelConfig? GetRiskLevelConfig(double score)
    {
        var levels = GetLevels();
        if (levels is null) return null;

        foreach (var level in levels)
        {
            if (score >= level.Min && (level.Max is null || score < level.Max.Value))
                return level;
        }
        return null;
    }

    private RiskLevelConfig[]? GetLevels() => config.Get<RiskLevelConfig[]>("fk_levels");
}
