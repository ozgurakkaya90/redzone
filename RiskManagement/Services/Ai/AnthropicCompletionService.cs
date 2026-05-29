using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RiskManagement.Models;

namespace RiskManagement.Services.Ai;

public sealed class AnthropicCompletionService(AiSettings settings, IHttpClientFactory httpFactory) : IAiCompletionService
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public bool IsAvailable => !string.IsNullOrWhiteSpace(settings.ApiKey);

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        var model = string.IsNullOrWhiteSpace(settings.Model) ? "claude-haiku-4-5-20251001" : settings.Model;

        var body = new
        {
            model,
            max_tokens = 1024,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };

        using var client = httpFactory.CreateClient();
        client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var request = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("https://api.anthropic.com/v1/messages", request, ct);

        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Anthropic API hatası ({response.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }
}
