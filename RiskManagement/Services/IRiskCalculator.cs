using RiskManagement.Models;

namespace RiskManagement.Services;

/// <summary>
/// Fine-Kinney risk hesaplama sözleşmesi.
/// Renk ve eşik bilgilerini ConfigService üzerinden DB'den alır; hardcoded değer içermez.
/// </summary>
public interface IRiskCalculator
{
    /// <summary>Fine-Kinney skoruna karşılık gelen risk seviyesi etiketini döner.</summary>
    string CalculateRiskLevel(double score);

    /// <summary>Risk seviyesi etiketine karşılık gelen hex renk kodunu DB konfigürasyonundan döner.</summary>
    string GetRiskLevelColor(string label);

    /// <summary>Skora tam olarak uyan RiskLevelConfig bandını döner; bulunamazsa null.</summary>
    RiskLevelConfig? GetRiskLevelConfig(double score);
}
