using System.Text.Json;
using RiskManagement.Models;

namespace RiskManagement.Services.Ai;

public record AiRiskDraftResult(
    string Title,
    string? Category,
    string? SourceType,
    string? Hazard,
    string? PossibleImpact);

public sealed class AiRiskDraftService(IAiCompletionService ai, ConfigService config, IHttpClientFactory _httpFactory)
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private const string SystemPrompt = """
        Sen bir kurumsal risk yönetimi uzmanısın. Kullanıcının tarif ettiği durumu analiz ederek
        profesyonel bir risk kaydı taslağı oluşturuyorsun.

        Yanıtını SADECE aşağıdaki JSON formatında ver, başka hiçbir metin ekleme:
        {
          "title": "Risk başlığı (açık ve öz, max 100 karakter)",
          "category": "Kategori — yalnızca şunlardan biri: Stratejik, Operasyonel, Finansal, Uyum, Bilgi Teknolojileri, İtibar, Çevre",
          "source_type": "internal veya external",
          "hazard": "Tehlike veya risk kaynağının kısa açıklaması",
          "possible_impact": "Olası etki ve sonuçların kısa açıklaması"
        }
        """;

    public bool IsAvailable => ai.IsAvailable;

    // Geçici ayarlarla bağlantı testi — kayıt gerekmez, UI'dan çağrılır.
    public async Task<string> TestConnectionAsync(string provider, string apiKey, string model, string baseUrl, CancellationToken ct = default)
    {
        var settings = new AiSettings(provider, apiKey, model, baseUrl);
        IAiCompletionService testSvc = provider switch
        {
            "anthropic"                      => new AnthropicCompletionService(settings, _httpFactory),
            "openai_compatible" or "openai" or "gemini" or "deepseek" or "ollama"
                        => new OpenAiCompatibleCompletionService(settings, _httpFactory),
            _                                => new NullCompletionService()
        };

        if (!testSvc.IsAvailable)
            throw new InvalidOperationException("API anahtarı girilmemiş.");

        var response = await testSvc.CompleteAsync(
            "Kısa yanıt ver.",
            "Merhaba, bağlantı testi. Sadece 'Bağlantı başarılı' yaz.",
            ct);

        var model2 = string.IsNullOrWhiteSpace(model)
            ? (provider switch { "openai" => "gpt-4o-mini", "gemini" => "gemini-1.5-flash", _ => provider })
            : model;
        return $"Model: {model2}";
    }

    public async Task<AiRiskDraftResult> DraftAsync(string userDescription, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userDescription))
            throw new ArgumentException("Açıklama boş olamaz.");

        // Sistem genelinde tanımlı kategorileri prompt'a ekle
        var categories = config.GetList("risk_categories");
        var systemPrompt = categories.Length > 0
            ? SystemPrompt.Replace(
                "Stratejik, Operasyonel, Finansal, Uyum, Bilgi Teknolojileri, İtibar, Çevre",
                string.Join(", ", categories))
            : SystemPrompt;

        var raw = await ai.CompleteAsync(systemPrompt, userDescription, ct);

        // Yanıt bazen ```json ... ``` bloğuyla sarılı gelebilir
        var json = ExtractJson(raw);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            return new AiRiskDraftResult(
                Title:          r.GetStringOrDefault("title") ?? userDescription[..Math.Min(100, userDescription.Length)],
                Category:       r.GetStringOrDefault("category"),
                SourceType:     r.GetStringOrDefault("source_type"),
                Hazard:         r.GetStringOrDefault("hazard"),
                PossibleImpact: r.GetStringOrDefault("possible_impact"));
        }
        catch (JsonException)
        {
            // AI yanıtı parse edilemezse minimum sonuç döndür
            return new AiRiskDraftResult(Title: userDescription[..Math.Min(100, userDescription.Length)],
                Category: null, SourceType: null, Hazard: null, PossibleImpact: null);
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end   = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}

file static class JsonElementExtensions
{
    public static string? GetStringOrDefault(this JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
