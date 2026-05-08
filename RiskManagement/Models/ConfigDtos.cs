using System.Text.Json.Serialization;

namespace RiskManagement.Models;

/// <summary>
/// Fine-Kinney olasılık / maruziyet / sonuç ağırlıklandırma seçeneği.
/// İleride çok dilli destek için Label/Hint alanları genişletilebilir
/// (örn. Dictionary&lt;string,string&gt; Labels).
/// </summary>
public sealed record ScoredOption(
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("hint")]  string Hint = "");

/// <summary>
/// Denetim türü gibi değer/etiket çifti gerektiren seçenekler.
/// Hiyerarşik yapı gerekirse ParentValue alanı eklenebilir.
/// </summary>
public sealed record LabeledOption(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label);

/// <summary>
/// Fine-Kinney risk seviyesi bant tanımı.
/// Renk ve aksiyon bilgisi DB'den geldiği için hardcoded değil.
/// </summary>
public sealed record RiskLevelConfig(
    [property: JsonPropertyName("min")]    double  Min,
    [property: JsonPropertyName("max")]    double? Max,
    [property: JsonPropertyName("label")]  string  Label,
    [property: JsonPropertyName("color")]  string  Color,
    [property: JsonPropertyName("action")] string  Action);

/// <summary>
/// Uygulama tema/branding ayarları.
/// </summary>
public sealed record SiteThemeConfig(
    [property: JsonPropertyName("primary_color")]   string PrimaryColor   = "#E1251B",
    [property: JsonPropertyName("secondary_color")] string SecondaryColor = "#16a34a",
    [property: JsonPropertyName("font_family")]     string FontFamily     = "system-ui,-apple-system,sans-serif",
    [property: JsonPropertyName("sidebar_color")]   string SidebarColor   = "#1a1a2e");
