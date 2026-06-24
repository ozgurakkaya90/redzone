using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RiskManagement.Models;

namespace RiskManagement.Services.Ai;

// OpenAI-uyumlu /v1/chat/completions protokolünü kullanan tüm sağlayıcılar için.
// Sağlayıcı adı yerine BaseUrl + Model + ApiKey üçlüsü kullanılır;
// böylece herhangi bir uyumlu API eklenmeden çalışır (Grok, Groq, OpenRouter…).
public sealed class OpenAiCompatibleCompletionService(AiSettings settings, IHttpClientFactory httpFactory) : IAiCompletionService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    // Geriye dönük uyumluluk: eski provider adlarına göre URL/model varsayılanları.
    private static readonly IReadOnlyDictionary<string, (string Url, string Model)> _legacyDefaults =
        new Dictionary<string, (string, string)>
        {
            ["openai"]   = ("https://api.openai.com/v1/",                              "gpt-4o-mini"),
            ["gemini"]   = ("https://generativelanguage.googleapis.com/v1beta/openai/", "gemini-2.0-flash"),
            ["deepseek"] = ("https://api.deepseek.com/v1/",                            "deepseek-chat"),
            ["ollama"]   = ("http://localhost:11434/v1/",                               "llama3"),
        };

    public bool IsAvailable =>
        // Ollama ve API anahtarı gerektirmeyen yerel modeller için boş key geçerli
        string.IsNullOrWhiteSpace(settings.ApiKey)
            ? !string.IsNullOrWhiteSpace(settings.BaseUrl)   // URL varsa yerel sayılır
            : true;

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        // Kullanıcının girdiği URL/model öncelikli; yoksa eski provider adından varsayılan al
        _legacyDefaults.TryGetValue(settings.Provider, out var leg);
        var baseUrl = !string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? settings.BaseUrl.TrimEnd('/') + "/"
            : (leg.Url ?? "http://localhost:11434/v1/");

        var model = !string.IsNullOrWhiteSpace(settings.Model)
            ? settings.Model
            : (leg.Model ?? "gpt-4o-mini");

        var body = new
        {
            model,
            max_tokens = 1024,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userMessage  }
            }
        };

        using var client = httpFactory.CreateClient("ai");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var request = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(baseUrl + "chat/completions", request, ct);

        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"AI API hatası ({response.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }
}
